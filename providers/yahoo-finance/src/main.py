#!/usr/bin/env python3
"""Yahoo Finance TimeBase Provider

A production-ready provider that fetches real financial data from Yahoo Finance
using direct API calls with robust rate limiting and retry logic.
Supports historical OHLCV data for stocks, ETFs, and indices.
Includes polling-based real-time data streaming.
"""

import asyncio
import time
import os
from datetime import datetime, timedelta, timezone
from typing import Optional, AsyncGenerator, AsyncIterator, Dict, Set, Any, List
import logging
import random
import json

import requests
from requests.adapters import HTTPAdapter
from urllib3.util.retry import Retry
import pandas as pd
from timebase_sdk import TimeBaseProvider, run_grpc_server


# Yahoo Finance API endpoints
YF_BASE_URL = "https://query1.finance.yahoo.com"
YF_CHART_URL = f"{YF_BASE_URL}/v8/finance/chart"
YF_QUOTE_URL = f"{YF_BASE_URL}/v7/finance/quote"


def create_robust_session() -> requests.Session:
    """Create a requests session with retry logic and proper headers."""
    session = requests.Session()
    
    # Retry strategy - handle transient errors
    retry_strategy = Retry(
        total=3,
        backoff_factor=1,
        status_forcelist=[500, 502, 503, 504],
        allowed_methods=["HEAD", "GET", "OPTIONS"],
        raise_on_status=False
    )
    
    adapter = HTTPAdapter(
        max_retries=retry_strategy, 
        pool_connections=3,
        pool_maxsize=3
    )
    session.mount("http://", adapter)
    session.mount("https://", adapter)
    
    # Set headers to mimic browser
    user_agents = [
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
        'Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36',
        'Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:121.0) Gecko/20100101 Firefox/121.0',
    ]
    
    session.headers.update({
        'User-Agent': random.choice(user_agents),
        'Accept': 'application/json,text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8',
        'Accept-Language': 'en-US,en;q=0.5',
        'Accept-Encoding': 'gzip, deflate, br',
        'Connection': 'keep-alive',
        'Cache-Control': 'no-cache',
    })
    
    return session


class YahooFinanceAPI:
    """Direct Yahoo Finance API client with proper rate limiting."""
    
    def __init__(self, logger: logging.Logger):
        self.logger = logger
        self._session = create_robust_session()
        self._last_request_time = 0
        self._request_count_minute = 0
        self._minute_reset_time = time.time()
        self._rate_limit_until = 0
        
        # Very conservative rate limits
        self.MIN_REQUEST_INTERVAL = 5.0  # 5 seconds between requests
        self.MAX_REQUESTS_PER_MINUTE = 8  # Max 8 per minute
        
    async def _wait_for_rate_limit(self):
        """Wait if rate limited."""
        current_time = time.time()
        
        # Check cooldown period
        if current_time < self._rate_limit_until:
            wait_time = self._rate_limit_until - current_time
            self.logger.info(f"Rate limit cooldown: waiting {wait_time:.1f}s")
            await asyncio.sleep(wait_time)
            current_time = time.time()
        
        # Reset minute counter
        if current_time - self._minute_reset_time >= 60:
            self._request_count_minute = 0
            self._minute_reset_time = current_time
        
        # Check minute limit
        if self._request_count_minute >= self.MAX_REQUESTS_PER_MINUTE:
            wait_time = 60 - (current_time - self._minute_reset_time) + random.uniform(5, 15)
            self.logger.warning(f"Minute rate limit reached, waiting {wait_time:.1f}s")
            await asyncio.sleep(wait_time)
            self._request_count_minute = 0
            self._minute_reset_time = time.time()
        
        # Ensure minimum interval between requests
        time_since_last = current_time - self._last_request_time
        if time_since_last < self.MIN_REQUEST_INTERVAL:
            wait_time = self.MIN_REQUEST_INTERVAL - time_since_last + random.uniform(1, 3)
            await asyncio.sleep(wait_time)
        
        self._request_count_minute += 1
        self._last_request_time = time.time()
    
    def _handle_429(self):
        """Handle rate limit response."""
        cooldown = random.uniform(120, 180)  # 2-3 minutes cooldown
        self._rate_limit_until = time.time() + cooldown
        self.logger.warning(f"Rate limit hit (429)! Cooling down for {cooldown:.0f}s")
    
    def _map_interval(self, interval: str) -> str:
        """Map interval to Yahoo Finance format."""
        interval_map = {
            "1m": "1m", "2m": "2m", "5m": "5m", "15m": "15m",
            "30m": "30m", "60m": "60m", "90m": "90m", "1h": "1h",
            "1d": "1d", "5d": "5d", "1wk": "1wk", "1mo": "1mo", "3mo": "3mo"
        }
        return interval_map.get(interval, "1d")
    
    def _get_range_for_interval(self, interval: str) -> str:
        """Get appropriate range for an interval."""
        range_map = {
            "1m": "1d", "2m": "1d", "5m": "5d", "15m": "5d",
            "30m": "1mo", "60m": "1mo", "90m": "1mo", "1h": "1mo",
            "1d": "1y", "5d": "5y", "1wk": "5y", "1mo": "max", "3mo": "max"
        }
        return range_map.get(interval, "1mo")
    
    async def fetch_chart_data(
        self,
        symbol: str,
        interval: str = "1d",
        period1: Optional[int] = None,
        period2: Optional[int] = None,
        range_str: Optional[str] = None
    ) -> Optional[List[dict]]:
        """
        Fetch chart data from Yahoo Finance API.
        
        Args:
            symbol: Stock symbol (e.g., AAPL, BTC-USD)
            interval: Data interval (1m, 5m, 15m, 1h, 1d, etc.)
            period1: Start timestamp (Unix)
            period2: End timestamp (Unix)
            range_str: Alternative to period1/period2 (1d, 5d, 1mo, 3mo, 1y, 5y, max)
            
        Returns:
            List of OHLCV data points or None if failed
        """
        await self._wait_for_rate_limit()
        
        yf_interval = self._map_interval(interval)
        
        # Build URL
        url = f"{YF_CHART_URL}/{symbol}"
        params = {
            "interval": yf_interval,
            "includePrePost": "false",
            "events": "div,splits",
        }
        
        if period1 and period2:
            params["period1"] = period1
            params["period2"] = period2
        elif range_str:
            params["range"] = range_str
        else:
            params["range"] = self._get_range_for_interval(interval)
        
        self.logger.info(f"Fetching {symbol} with interval={yf_interval}, params={params}")
        
        try:
            loop = asyncio.get_event_loop()
            response = await loop.run_in_executor(
                None,
                lambda: self._session.get(url, params=params, timeout=30)
            )
            
            if response.status_code == 429:
                self._handle_429()
                return None
            
            if response.status_code != 200:
                self.logger.warning(f"API returned {response.status_code}: {response.text[:200]}")
                return None
            
            data = response.json()
            
            # Parse response
            chart = data.get("chart", {})
            result = chart.get("result", [])
            
            if not result:
                error = chart.get("error", {})
                if error:
                    self.logger.warning(f"API error for {symbol}: {error}")
                return None
            
            result = result[0]
            timestamps = result.get("timestamp", [])
            indicators = result.get("indicators", {})
            quote = indicators.get("quote", [{}])[0]
            
            if not timestamps:
                self.logger.warning(f"No timestamps in response for {symbol}")
                return None
            
            # Build data points
            opens = quote.get("open", [])
            highs = quote.get("high", [])
            lows = quote.get("low", [])
            closes = quote.get("close", [])
            volumes = quote.get("volume", [])
            
            data_points = []
            for i, ts in enumerate(timestamps):
                if ts is None:
                    continue
                    
                o = opens[i] if i < len(opens) else None
                h = highs[i] if i < len(highs) else None
                l = lows[i] if i < len(lows) else None
                c = closes[i] if i < len(closes) else None
                v = volumes[i] if i < len(volumes) else None
                
                # Skip if any OHLC value is None
                if any(x is None for x in [o, h, l, c]):
                    continue
                
                data_points.append({
                    "timestamp": datetime.fromtimestamp(ts, tz=timezone.utc),
                    "open": float(o),
                    "high": float(h),
                    "low": float(l),
                    "close": float(c),
                    "volume": int(v) if v else 0,
                })
            
            self.logger.info(f"Parsed {len(data_points)} data points for {symbol}")
            return data_points
            
        except requests.exceptions.RequestException as e:
            self.logger.error(f"Request error for {symbol}: {e}")
            return None
        except json.JSONDecodeError as e:
            self.logger.error(f"JSON decode error for {symbol}: {e}")
            return None
        except Exception as e:
            self.logger.error(f"Unexpected error for {symbol}: {e}", exc_info=True)
            return None
    
    def recreate_session(self):
        """Recreate HTTP session (useful after errors)."""
        self._session = create_robust_session()
        self.logger.info("HTTP session recreated")


class YahooFinanceProvider(TimeBaseProvider):
    """Yahoo Finance data provider for TimeBase.
    
    This provider fetches real historical OHLCV data from Yahoo Finance
    using direct API calls with robust rate limiting.
    """

    def __init__(self):
        super().__init__()
        self._api = YahooFinanceAPI(self.logger)
        
        # Real-time streaming state
        self._active_subscriptions: Dict[str, Set[str]] = {}
        self._last_prices: Dict[str, dict] = {}
        self._streaming_paused = False
        self._consecutive_failures: Dict[str, int] = {}
        self._max_consecutive_failures = 5

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
            "rate_limits": {
                "requests_per_minute": self._api.MAX_REQUESTS_PER_MINUTE,
                "min_interval_seconds": self._api.MIN_REQUEST_INTERVAL
            },
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
        """Fetch historical OHLCV data from Yahoo Finance."""
        self.logger.info(
            f"Fetching historical data for {symbol} "
            f"from {start_time} to {end_time} at {interval} interval"
        )
        
        # Convert to Unix timestamps
        period1 = int(start_time.timestamp())
        period2 = int(end_time.timestamp())
        
        data_points = await self._api.fetch_chart_data(
            symbol=symbol,
            interval=interval,
            period1=period1,
            period2=period2
        )
        
        if not data_points:
            self.logger.warning(f"No data returned for {symbol}")
            return
        
        self.logger.info(f"Fetched {len(data_points)} data points for {symbol}")
        
        count = 0
        for point in data_points:
            if limit and count >= limit:
                break
            
            yield {
                "symbol": symbol,
                "timestamp": point["timestamp"],
                "open": point["open"],
                "high": point["high"],
                "low": point["low"],
                "close": point["close"],
                "volume": point["volume"],
                "interval": interval,
                "provider": self.config.slug,
                "metadata": {
                    "source": "yahoo_finance_api",
                    "adjusted": True,
                    "quality": "production"
                }
            }
            count += 1
            await asyncio.sleep(0.001)

    async def stream_realtime_data(
        self,
        subscriptions: AsyncIterator[dict]
    ) -> AsyncIterator[dict]:
        """Stream real-time data using polling."""
        self.logger.info("Starting real-time streaming service (polling-based)")
        
        subscription_task = asyncio.create_task(
            self._process_subscriptions(subscriptions)
        )
        
        try:
            async for data_point in self._polling_loop():
                yield data_point
        finally:
            subscription_task.cancel()
            try:
                await subscription_task
            except asyncio.CancelledError:
                pass
            self.logger.info("Real-time streaming service stopped")

    async def _process_subscriptions(self, subscriptions: AsyncIterator[dict]) -> None:
        """Process subscription control messages."""
        try:
            async for msg in subscriptions:
                action = msg.get("action", "").upper()
                symbol = msg.get("symbol", "").upper()
                interval = msg.get("interval", "1m")
                
                self.logger.info(f"Subscription message: {action} {symbol}/{interval}")
                
                if action == "SUBSCRIBE":
                    if symbol not in self._active_subscriptions:
                        self._active_subscriptions[symbol] = set()
                    self._active_subscriptions[symbol].add(interval)
                    
                elif action == "UNSUBSCRIBE":
                    if symbol in self._active_subscriptions:
                        self._active_subscriptions[symbol].discard(interval)
                        if not self._active_subscriptions[symbol]:
                            del self._active_subscriptions[symbol]
                    
                elif action == "PAUSE":
                    self._streaming_paused = True
                    
                elif action == "RESUME":
                    self._streaming_paused = False
                    
        except asyncio.CancelledError:
            pass
        except Exception as e:
            self.logger.error(f"Error processing subscriptions: {e}", exc_info=True)

    async def _polling_loop(self) -> AsyncIterator[dict]:
        """Main polling loop for real-time data."""
        # Very conservative polling intervals to avoid rate limits
        poll_delays = {
            "1m": 180,     # 3 minutes
            "2m": 240,     # 4 minutes
            "5m": 360,     # 6 minutes
            "15m": 900,    # 15 minutes
            "30m": 1800,   # 30 minutes
            "60m": 3600,   # 1 hour
            "1h": 3600,    # 1 hour
            "1d": 7200,    # 2 hours
        }
        
        last_poll_times: Dict[str, float] = {}
        
        while True:
            if self._streaming_paused:
                await asyncio.sleep(10)
                continue
            
            # Check rate limit cooldown
            if time.time() < self._api._rate_limit_until:
                wait_time = self._api._rate_limit_until - time.time()
                self.logger.info(f"Polling paused for rate limit: {wait_time:.0f}s remaining")
                await asyncio.sleep(min(wait_time + 5, 60))
                continue
            
            current_time = time.time()
            subscriptions = dict(self._active_subscriptions)
            
            if not subscriptions:
                await asyncio.sleep(10)
                continue
            
            for symbol, intervals in subscriptions.items():
                for interval in intervals:
                    key = f"{symbol}:{interval}"
                    poll_delay = poll_delays.get(interval, 600)
                    last_poll = last_poll_times.get(key, 0)
                    
                    if current_time - last_poll < poll_delay:
                        continue
                    
                    # Check consecutive failures
                    if self._consecutive_failures.get(symbol, 0) >= self._max_consecutive_failures:
                        self.logger.warning(f"Skipping {symbol} due to consecutive failures")
                        self._consecutive_failures[symbol] = 0
                        continue
                    
                    try:
                        data_point = await self._fetch_latest_price(symbol, interval)
                        
                        if data_point:
                            last_data = self._last_prices.get(key)
                            if last_data is None or self._has_price_changed(last_data, data_point):
                                self._last_prices[key] = data_point
                                self._consecutive_failures[symbol] = 0
                                yield data_point
                        else:
                            self._consecutive_failures[symbol] = self._consecutive_failures.get(symbol, 0) + 1
                        
                        last_poll_times[key] = time.time()
                        
                    except Exception as e:
                        self.logger.error(f"Error fetching {symbol}: {e}")
                        self._consecutive_failures[symbol] = self._consecutive_failures.get(symbol, 0) + 1
                        last_poll_times[key] = time.time()
                    
                    # Delay between symbols
                    await asyncio.sleep(random.uniform(5, 10))
            
            await asyncio.sleep(10)

    def _has_price_changed(self, old: dict, new: dict) -> bool:
        """Check if price data has changed."""
        return (
            old.get("open") != new.get("open") or
            old.get("high") != new.get("high") or
            old.get("low") != new.get("low") or
            old.get("close") != new.get("close") or
            old.get("volume") != new.get("volume") or
            old.get("timestamp") != new.get("timestamp")
        )

    async def _fetch_latest_price(self, symbol: str, interval: str) -> Optional[dict]:
        """Fetch the latest price for a symbol."""
        range_str = "1d" if interval in ["1m", "2m", "5m", "15m", "30m", "60m", "90m", "1h"] else "5d"
        
        data_points = await self._api.fetch_chart_data(
            symbol=symbol,
            interval=interval,
            range_str=range_str
        )
        
        if not data_points:
            return None
        
        # Get most recent point
        latest = data_points[-1]
        
        return {
            "symbol": symbol,
            "timestamp": latest["timestamp"],
            "open": latest["open"],
            "high": latest["high"],
            "low": latest["low"],
            "close": latest["close"],
            "volume": latest["volume"],
            "interval": interval,
            "provider": self.config.slug,
            "metadata": {
                "source": "yahoo_finance_api",
                "adjusted": True,
                "quality": "realtime_poll",
                "poll_time": datetime.now(timezone.utc).isoformat()
            }
        }


def main():
    """Main entry point."""
    print("Starting Yahoo Finance TimeBase Provider (Direct API)...")
    
    provider = YahooFinanceProvider()
    print(f"Provider: {provider.config.name} v{provider.config.version}")
    print(f"Slug: {provider.config.slug}")
    print(f"Rate Limits: {provider._api.MAX_REQUESTS_PER_MINUTE}/min, {provider._api.MIN_REQUEST_INTERVAL}s interval")
    
    run_grpc_server(provider, port=50051)


if __name__ == "__main__":
    main()
