#!/usr/bin/env python3
"""Yahoo Finance TimeBase Provider

A production-ready provider that fetches real financial data from Yahoo Finance
using the yfinance library (https://github.com/ranaroussi/yfinance).
Supports historical OHLCV data and real-time streaming via WebSocket for stocks, ETFs, indices, and crypto.
"""

import asyncio
import logging
from datetime import datetime, timezone
from typing import Optional, AsyncGenerator, AsyncIterator, Dict, Set, Any
import time

import yfinance as yf
import pandas as pd
from timebase_sdk import TimeBaseProvider, run_grpc_server


class YahooFinanceProvider(TimeBaseProvider):
    """Yahoo Finance data provider for TimeBase.
    
    This provider fetches real historical OHLCV data from Yahoo Finance
    using the yfinance library with WebSocket support for real-time streaming.
    """

    def __init__(self):
        super().__init__()
        
        # Real-time streaming state
        self._websocket: Optional[yf.AsyncWebSocket] = None
        self._active_subscriptions: Set[str] = set()
        self._streaming_paused = False
        self._message_queue: Optional[asyncio.Queue] = None
        self._ws_task: Optional[asyncio.Task] = None

    async def get_capabilities(self) -> dict:
        """Return provider capabilities."""
        return {
            "name": self.config.name,
            "version": self.config.version,
            "slug": self.config.slug,
            "supports_historical": True,
            "supports_realtime": True,
            "supports_backfill": True,
            "data_types": ["stocks", "etf", "index", "crypto"],
            "intervals": ["1m", "2m", "5m", "15m", "30m", "60m", "90m", "1h", "1d", "5d", "1wk", "1mo", "3mo"],
            "max_lookback_days": 3650
        }

    async def get_historical_data(
        self,
        symbol: str,
        interval: str,
        start_time: datetime,
        end_time: datetime,
        limit: Optional[int] = None
    ) -> AsyncGenerator[dict, None]:
        """Fetch historical OHLCV data from Yahoo Finance using yfinance."""
        self.logger.info(
            f"Fetching historical data for {symbol} "
            f"from {start_time} to {end_time} at {interval} interval"
        )
        
        try:
            # yfinance's history() is synchronous, run in executor to avoid blocking
            loop = asyncio.get_event_loop()
            ticker = yf.Ticker(symbol)
            
            df = await loop.run_in_executor(
                None,
                lambda: ticker.history(
                    start=start_time,
                    end=end_time,
                    interval=interval,
                    auto_adjust=True,
                    prepost=False,
                    actions=False,
                    raise_errors=False
                )
            )
            
            if df is None or df.empty:
                self.logger.warning(f"No data returned for {symbol}")
                return
            
            self.logger.info(f"Fetched {len(df)} data points for {symbol}")
            
            count = 0
            for timestamp, row in df.iterrows():
                if limit and count >= limit:
                    break
                
                # Handle missing values
                if pd.isna(row['Open']) or pd.isna(row['Close']):
                    continue
                
                # Convert timestamp to UTC datetime
                if hasattr(timestamp, 'to_pydatetime'):
                    ts = timestamp.to_pydatetime()
                else:
                    ts = timestamp
                
                # Ensure timezone-aware
                if ts.tzinfo is None:
                    ts = ts.replace(tzinfo=timezone.utc)
                
                yield {
                    "symbol": symbol,
                    "timestamp": ts,
                    "open": float(row['Open']),
                    "high": float(row['High']),
                    "low": float(row['Low']),
                    "close": float(row['Close']),
                    "volume": int(row['Volume']) if not pd.isna(row['Volume']) else 0,
                    "interval": interval,
                    "provider": self.config.slug,
                    "metadata": {
                        "source": "yfinance",
                        "adjusted": "true",
                        "quality": "production"
                    }
                }
                count += 1
                await asyncio.sleep(0)  # Yield control
                
        except Exception as e:
            self.logger.error(f"Error fetching historical data for {symbol}: {e}", exc_info=True)
            return

    async def stream_realtime_data(
        self,
        subscriptions: AsyncIterator[dict]
    ) -> AsyncIterator[dict]:
        """Stream real-time data using Yahoo Finance WebSocket."""
        self.logger.info("Starting real-time streaming service (WebSocket-based)")
        
        # Create message queue for WebSocket messages
        self._message_queue = asyncio.Queue()
        
        # Start subscription processor
        subscription_task = asyncio.create_task(
            self._process_subscriptions(subscriptions)
        )
        
        try:
            # Yield messages from the queue
            while True:
                try:
                    # Wait for messages with timeout to check for shutdown
                    message = await asyncio.wait_for(
                        self._message_queue.get(),
                        timeout=1.0
                    )
                    
                    if message is None:  # Shutdown signal
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
            
            # Clean up WebSocket
            if self._websocket:
                try:
                    await self._websocket.close()
                except Exception as e:
                    self.logger.warning(f"Error closing WebSocket: {e}")
                self._websocket = None
            
            self.logger.info("Real-time streaming service stopped")

    async def _process_subscriptions(self, subscriptions: AsyncIterator[dict]) -> None:
        """Process subscription control messages."""
        try:
            async for msg in subscriptions:
                action = msg.get("action", "").upper()
                symbol = msg.get("symbol", "").upper()
                
                self.logger.info(f"Subscription message: {action} {symbol}")
                
                if action == "SUBSCRIBE":
                    await self._subscribe_symbol(symbol)
                    
                elif action == "UNSUBSCRIBE":
                    await self._unsubscribe_symbol(symbol)
                    
                elif action == "PAUSE":
                    self._streaming_paused = True
                    self.logger.info("Real-time streaming paused")
                    
                elif action == "RESUME":
                    self._streaming_paused = False
                    self.logger.info("Real-time streaming resumed")
                    
        except asyncio.CancelledError:
            pass
        except Exception as e:
            self.logger.error(f"Error processing subscriptions: {e}", exc_info=True)

    async def _subscribe_symbol(self, symbol: str) -> None:
        """Subscribe to a symbol via WebSocket."""
        if symbol in self._active_subscriptions:
            self.logger.debug(f"Already subscribed to {symbol}")
            return
        
        self._active_subscriptions.add(symbol)
        
        # Initialize WebSocket if not already running
        if self._websocket is None:
            await self._start_websocket()
        
        # Subscribe to the symbol
        try:
            await self._websocket.subscribe(symbol)
            self.logger.info(f"Subscribed to {symbol}")
        except Exception as e:
            self.logger.error(f"Error subscribing to {symbol}: {e}")
            self._active_subscriptions.discard(symbol)

    async def _unsubscribe_symbol(self, symbol: str) -> None:
        """Unsubscribe from a symbol via WebSocket."""
        if symbol not in self._active_subscriptions:
            return
        
        self._active_subscriptions.discard(symbol)
        
        if self._websocket:
            try:
                await self._websocket.unsubscribe(symbol)
                self.logger.info(f"Unsubscribed from {symbol}")
            except Exception as e:
                self.logger.error(f"Error unsubscribing from {symbol}: {e}")
        
        # Close WebSocket if no active subscriptions
        if not self._active_subscriptions and self._websocket:
            await self._websocket.close()
            self._websocket = None
            self.logger.info("WebSocket closed (no active subscriptions)")

    async def _start_websocket(self) -> None:
        """Initialize and start the WebSocket connection."""
        if self._websocket:
            return
        
        try:
            self.logger.info("Initializing WebSocket connection")
            self._websocket = yf.AsyncWebSocket(verbose=False)
            
            # Start listening in background
            self._ws_task = asyncio.create_task(
                self._websocket.listen(self._handle_websocket_message)
            )
            
            self.logger.info("WebSocket connection established")
            
        except Exception as e:
            self.logger.error(f"Error starting WebSocket: {e}", exc_info=True)
            self._websocket = None

    def _handle_websocket_message(self, message: dict) -> None:
        """Handle incoming WebSocket messages and convert to TimeBase format."""
        if self._streaming_paused or not self._message_queue:
            return
        
        try:
            # Extract symbol and price data from yfinance WebSocket message
            # The message format from yfinance WebSocket typically includes:
            # - id: symbol
            # - price: last price
            # - time: timestamp
            # - dayVolume: volume
            # - change, changePercent, etc.
            
            symbol = message.get('id', '').upper()
            if not symbol or symbol not in self._active_subscriptions:
                return
            
            price = message.get('price')
            if price is None:
                return
            
            # Get timestamp
            msg_time = message.get('time')
            if msg_time:
                if isinstance(msg_time, (int, float)):
                    # Unix timestamp in seconds
                    timestamp = datetime.fromtimestamp(msg_time, tz=timezone.utc)
                else:
                    timestamp = datetime.now(timezone.utc)
            else:
                timestamp = datetime.now(timezone.utc)
            
            # For tick data, we create a simplified OHLCV where open=high=low=close=price
            # The metadata indicates this is tick data, not a full candle
            volume = message.get('dayVolume', 0)
            
            data_point = {
                "symbol": symbol,
                "timestamp": timestamp,
                "open": float(price),
                "high": float(price),
                "low": float(price),
                "close": float(price),
                "volume": int(volume) if volume else 0,
                "interval": "tick",  # Indicates real-time tick data
                "provider": self.config.slug,
                "metadata": {
                    "source": "yfinance_websocket",
                    "quality": "realtime",
                    "type": "tick",
                    "change": str(message.get('change', '')),
                    "change_percent": str(message.get('changePercent', ''))
                }
            }
            
            # Put message in queue (non-blocking)
            try:
                self._message_queue.put_nowait(data_point)
            except asyncio.QueueFull:
                self.logger.warning(f"Message queue full, dropping tick for {symbol}")
                
        except Exception as e:
            self.logger.error(f"Error handling WebSocket message: {e}", exc_info=True)


def main():
    """Main entry point."""
    print("Starting Yahoo Finance TimeBase Provider (yfinance library)...")
    
    provider = YahooFinanceProvider()
    print(f"Provider: {provider.config.name} v{provider.config.version}")
    print(f"Slug: {provider.config.slug}")
    print("Using yfinance library for data fetching and WebSocket streaming")
    
    run_grpc_server(provider, port=50051)


if __name__ == "__main__":
    main()
