#!/usr/bin/env python3
"""CCXT TimeBase Provider

Multi-exchange crypto data provider using CCXT. Routes requests by
EXCHANGE:SYMBOL format (e.g., BINANCE:BTCUSDT).
"""

import asyncio
import os
from datetime import datetime, timezone
from typing import AsyncGenerator, AsyncIterator, Dict, Optional, Set, Tuple

import ccxt

from timebase_sdk import TimeBaseProvider, run_grpc_server


class CcxtProvider(TimeBaseProvider):
    """CCXT provider for TimeBase."""

    def __init__(self, config_path: str = "config.yaml"):
        super().__init__(config_path)
        self._exchanges: Dict[str, ccxt.Exchange] = {}
        self._stream_tasks: Dict[str, asyncio.Task] = {}
        self._stream_queue: Optional[asyncio.Queue] = None
        self._active_subscriptions: Set[str] = set()
        self._streaming_paused = False
        self._poll_interval_seconds = int(self.config.options.get("poll_interval_seconds", 5))

    async def get_capabilities(self) -> dict:
        return {
            "name": self.config.name,
            "version": self.config.version,
            "slug": self.config.slug,
            "supports_historical": True,
            "supports_realtime": True,
            "supports_backfill": True,
            "data_types": ["crypto"],
            "intervals": ["1m", "5m", "15m", "30m", "1h", "4h", "1d", "1wk", "1mo"],
            "max_lookback_days": 3650
        }

    async def get_symbols(self) -> list[dict]:
        """Return symbols supported by the default exchange (Binance)."""
        exchange = await self._get_exchange("binance")
        symbols = []

        for market in exchange.markets.values():
            market_symbol = market.get("id") or market.get("symbol")
            if not market_symbol:
                continue

            symbols.append({
                "symbol": f"BINANCE:{market_symbol}",
                "name": market.get("base") or market.get("symbol") or market_symbol,
                "type": "crypto",
                "intervals": self.config.intervals,
                "metadata": {
                    "exchange": "binance",
                    "base": str(market.get("base") or ""),
                    "quote": str(market.get("quote") or "")
                }
            })

        return symbols

    async def get_historical_data(
        self,
        symbol: str,
        interval: str,
        start_time: datetime,
        end_time: datetime,
        limit: Optional[int] = None
    ) -> AsyncGenerator[dict, None]:
        exchange_id, raw_symbol = self._parse_symbol(symbol)
        exchange = await self._get_exchange(exchange_id)

        self.logger.info(
            "Fetching historical data for %s on %s from %s to %s (%s)",
            raw_symbol, exchange_id, start_time, end_time, interval
        )

        if not exchange.has.get("fetchOHLCV", False):
            self.logger.warning("Exchange %s does not support OHLCV", exchange_id)
            return

        ccxt_symbol = await self._resolve_symbol(exchange, raw_symbol)

        since_ms = int(start_time.replace(tzinfo=timezone.utc).timestamp() * 1000)
        end_ms = int(end_time.replace(tzinfo=timezone.utc).timestamp() * 1000)
        page_limit = min(limit or 500, 1000)
        yielded = 0

        while True:
            loop = asyncio.get_running_loop()
            ohlcv = await loop.run_in_executor(
                None,
                lambda: exchange.fetch_ohlcv(ccxt_symbol, timeframe=interval, since=since_ms, limit=page_limit)
            )

            if not ohlcv:
                break

            for timestamp_ms, open_price, high, low, close, volume in ohlcv:
                if timestamp_ms > end_ms:
                    return

                timestamp = datetime.fromtimestamp(timestamp_ms / 1000, tz=timezone.utc)
                yield {
                    "symbol": symbol,
                    "timestamp": timestamp,
                    "open": float(open_price),
                    "high": float(high),
                    "low": float(low),
                    "close": float(close),
                    "volume": float(volume or 0),
                    "interval": interval,
                    "provider": self.config.slug,
                    "metadata": {
                        "source": "ccxt",
                        "exchange": exchange_id,
                        "market": ccxt_symbol
                    }
                }
                yielded += 1
                if limit and yielded >= limit:
                    return

            since_ms = ohlcv[-1][0] + 1
            if since_ms >= end_ms:
                break

            await asyncio.sleep(0)

    async def stream_realtime_data(
        self,
        subscriptions: AsyncIterator[dict]
    ) -> AsyncIterator[dict]:
        self._stream_queue = asyncio.Queue()
        subscription_task = asyncio.create_task(self._process_subscriptions(subscriptions))

        try:
            while True:
                try:
                    message = await asyncio.wait_for(self._stream_queue.get(), timeout=1.0)
                    if message is None:
                        break
                    yield message
                except asyncio.TimeoutError:
                    continue
        finally:
            subscription_task.cancel()
            try:
                await subscription_task
            except asyncio.CancelledError:
                pass

            for task in self._stream_tasks.values():
                task.cancel()
            self._stream_tasks.clear()

    async def health_check(self) -> dict:
        try:
            exchange = await self._get_exchange("binance")
            loop = asyncio.get_running_loop()
            if exchange.has.get("fetchStatus", False):
                await loop.run_in_executor(None, exchange.fetch_status)
            else:
                await loop.run_in_executor(None, exchange.fetch_time)
            return {
                "status": "HEALTHY",
                "message": "CCXT provider operational",
                "timestamp": datetime.utcnow()
            }
        except Exception as ex:
            return {
                "status": "DEGRADED",
                "message": f"Health check failed: {ex}",
                "timestamp": datetime.utcnow()
            }

    async def _process_subscriptions(self, subscriptions: AsyncIterator[dict]) -> None:
        try:
            async for msg in subscriptions:
                action = msg.get("action", "").upper()
                symbol = msg.get("symbol", "")
                interval = msg.get("interval", "")

                if not symbol:
                    continue

                if action == "SUBSCRIBE":
                    await self._subscribe_symbol(symbol, interval)
                elif action == "UNSUBSCRIBE":
                    await self._unsubscribe_symbol(symbol, interval)
                elif action == "PAUSE":
                    self._streaming_paused = True
                elif action == "RESUME":
                    self._streaming_paused = False
        except asyncio.CancelledError:
            pass

    async def _subscribe_symbol(self, symbol: str, interval: str) -> None:
        subscription_key = f"{symbol}|{interval}"
        if subscription_key in self._active_subscriptions:
            return

        self._active_subscriptions.add(subscription_key)
        exchange_id, raw_symbol = self._parse_symbol(symbol)
        exchange = await self._get_exchange(exchange_id)

        stream_key = f"{exchange_id}:{raw_symbol}:{interval}"
        if stream_key not in self._stream_tasks:
            self._stream_tasks[stream_key] = asyncio.create_task(
                self._stream_symbol(exchange_id, raw_symbol, interval, subscription_key, exchange)
            )

    async def _unsubscribe_symbol(self, symbol: str, interval: str) -> None:
        subscription_key = f"{symbol}|{interval}"
        self._active_subscriptions.discard(subscription_key)
        exchange_id, raw_symbol = self._parse_symbol(symbol)
        stream_key = f"{exchange_id}:{raw_symbol}:{interval}"
        task = self._stream_tasks.pop(stream_key, None)
        if task:
            task.cancel()

    async def _stream_symbol(
        self,
        exchange_id: str,
        raw_symbol: str,
        interval: str,
        subscription_key: str,
        exchange: ccxt.Exchange
    ) -> None:
        if not self._stream_queue:
            return

        ccxt_symbol = await self._resolve_symbol(exchange, raw_symbol)

        while True:
            try:
                if subscription_key not in self._active_subscriptions:
                    return

                if self._streaming_paused:
                    await asyncio.sleep(1)
                    continue

                if exchange.has.get("watchOHLCV", False):
                    timeframe = interval or "1m"
                    data = await exchange.watch_ohlcv(ccxt_symbol, timeframe=timeframe)
                    timestamp_ms, open_price, high, low, close, volume = data[-1]
                elif exchange.has.get("watchTicker", False):
                    ticker = await exchange.watch_ticker(ccxt_symbol)
                    timestamp_ms = ticker.get("timestamp") or int(datetime.now(timezone.utc).timestamp() * 1000)
                    open_price = ticker.get("open") or ticker.get("last") or 0
                    high = ticker.get("high") or open_price
                    low = ticker.get("low") or open_price
                    close = ticker.get("last") or open_price
                    volume = ticker.get("baseVolume") or 0
                    interval = "tick"
                else:
                    await asyncio.sleep(self._poll_interval_seconds)
                    continue

                timestamp = datetime.fromtimestamp(timestamp_ms / 1000, tz=timezone.utc)
                message = {
                    "symbol": f"{exchange_id.upper()}:{raw_symbol}",
                    "timestamp": timestamp,
                    "open": float(open_price),
                    "high": float(high),
                    "low": float(low),
                    "close": float(close),
                    "volume": float(volume or 0),
                    "interval": interval or "1m",
                    "provider": self.config.slug,
                    "metadata": {
                        "source": "ccxt_stream",
                        "exchange": exchange_id,
                        "market": ccxt_symbol
                    }
                }

                try:
                    self._stream_queue.put_nowait(message)
                except asyncio.QueueFull:
                    self.logger.warning("Stream queue full, dropping data")
            except asyncio.CancelledError:
                return
            except Exception as ex:
                self.logger.warning("Streaming error for %s:%s: %s", exchange_id, raw_symbol, ex)
                await asyncio.sleep(self._poll_interval_seconds)

    async def _get_exchange(self, exchange_id: str) -> ccxt.Exchange:
        exchange_key = exchange_id.lower()
        if exchange_key in self._exchanges:
            return self._exchanges[exchange_key]

        if not hasattr(ccxt, exchange_key):
            raise ValueError(f"Unknown exchange: {exchange_id}")

        exchange_class = getattr(ccxt, exchange_key)

        api_key = os.getenv(f"CCXT_{exchange_id.upper()}_API_KEY")
        api_secret = os.getenv(f"CCXT_{exchange_id.upper()}_SECRET")

        options: Dict[str, object] = {
            "enableRateLimit": True,
        }

        if api_key and api_secret:
            options["apiKey"] = api_key
            options["secret"] = api_secret

        exchange = exchange_class(options)
        loop = asyncio.get_running_loop()
        await loop.run_in_executor(None, exchange.load_markets)

        self._exchanges[exchange_key] = exchange
        return exchange

    def _parse_symbol(self, symbol: str) -> Tuple[str, str]:
        if ":" not in symbol:
            raise ValueError("Symbol must be in EXCHANGE:SYMBOL format")

        exchange_id, raw_symbol = symbol.split(":", 1)
        if not exchange_id or not raw_symbol:
            raise ValueError("Symbol must be in EXCHANGE:SYMBOL format")

        return exchange_id.lower(), raw_symbol

    async def _resolve_symbol(self, exchange: ccxt.Exchange, raw_symbol: str) -> str:
        if exchange.markets_by_id:
            market = exchange.markets_by_id.get(raw_symbol)
            if market:
                if isinstance(market, list):
                    return market[0]["symbol"]
                return market["symbol"]

        if raw_symbol in exchange.markets:
            return exchange.markets[raw_symbol]["symbol"]

        for market in exchange.markets.values():
            if market.get("id") == raw_symbol:
                return market["symbol"]

        raise ValueError(f"Unknown symbol for exchange: {raw_symbol}")


def main():
    print("Starting CCXT TimeBase Provider...")

    provider = CcxtProvider()
    print(f"Provider: {provider.config.name} v{provider.config.version}")
    print(f"Slug: {provider.config.slug}")
    print("Routing via EXCHANGE:SYMBOL format (e.g., BINANCE:BTCUSDT)")

    run_grpc_server(provider, port=50051)


if __name__ == "__main__":
    main()
