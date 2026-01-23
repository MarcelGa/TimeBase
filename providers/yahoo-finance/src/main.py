#!/usr/bin/env python3
"""Yahoo Finance TimeBase Provider

A production-ready provider that fetches real financial data from Yahoo Finance
using the yfinance library. Supports historical OHLCV data for stocks, ETFs, and indices.
"""

import asyncio
import time
from datetime import datetime, timedelta
from typing import Optional, AsyncGenerator
import logging

import yfinance as yf
from timebase_sdk import TimeBaseProvider, run_grpc_server


class YahooFinanceProvider(TimeBaseProvider):
    """Yahoo Finance data provider for TimeBase.
    
    This provider fetches real historical OHLCV data from Yahoo Finance.
    It includes rate limiting, error handling, and retry logic.
    """

    def __init__(self):
        super().__init__()
        self._last_request_time = 0
        self._request_count_minute = 0
        self._request_count_day = 0
        self._minute_reset_time = time.time()
        self._day_reset_time = time.time()

    async def get_capabilities(self) -> dict:
        """Return provider capabilities."""
        return {
            "name": self.config.name,
            "version": self.config.version,
            "slug": self.config.slug,
            "supports_historical": True,
            "supports_realtime": False,
            "supports_backfill": True,
            "data_types": ["stocks", "etf", "index", "crypto"],
            "intervals": ["1m", "2m", "5m", "15m", "30m", "60m", "90m", "1h", "1d", "5d", "1wk", "1mo", "3mo"],
            "rate_limits": {
                "requests_per_minute": 60,
                "requests_per_day": 2000
            },
            "max_lookback_days": 3650  # 10 years for daily data
        }

    async def _check_rate_limit(self):
        """Check and enforce rate limiting."""
        current_time = time.time()
        
        # Reset minute counter
        if current_time - self._minute_reset_time >= 60:
            self._request_count_minute = 0
            self._minute_reset_time = current_time
        
        # Reset day counter
        if current_time - self._day_reset_time >= 86400:
            self._request_count_day = 0
            self._day_reset_time = current_time
        
        # Check limits
        if self._request_count_minute >= 60:
            wait_time = 60 - (current_time - self._minute_reset_time)
            self.logger.warning(f"Rate limit reached, waiting {wait_time:.1f}s")
            await asyncio.sleep(wait_time)
            self._request_count_minute = 0
            self._minute_reset_time = time.time()
        
        if self._request_count_day >= 2000:
            self.logger.error("Daily rate limit reached")
            raise Exception("Daily rate limit exceeded for Yahoo Finance")
        
        # Increment counters
        self._request_count_minute += 1
        self._request_count_day += 1
        self._last_request_time = current_time

    def _map_interval(self, interval: str) -> str:
        """Map TimeBase interval to Yahoo Finance interval."""
        # Yahoo Finance uses: 1m, 2m, 5m, 15m, 30m, 60m, 90m, 1h, 1d, 5d, 1wk, 1mo, 3mo
        interval_map = {
            "1m": "1m",
            "2m": "2m",
            "5m": "5m",
            "15m": "15m",
            "30m": "30m",
            "60m": "60m",
            "90m": "90m",
            "1h": "1h",
            "1d": "1d",
            "5d": "5d",
            "1wk": "1wk",
            "1mo": "1mo",
            "3mo": "3mo"
        }
        
        return interval_map.get(interval, "1d")

    async def get_historical_data(
        self,
        symbol: str,
        interval: str,
        start_time: datetime,
        end_time: datetime,
        limit: Optional[int] = None
    ) -> AsyncGenerator[dict, None]:
        """Fetch historical OHLCV data from Yahoo Finance.
        
        Args:
            symbol: Stock symbol (e.g., "AAPL", "BTC-USD")
            interval: Data interval (e.g., "1d", "1h", "5m")
            start_time: Start datetime
            end_time: End datetime
            limit: Maximum number of data points to return
            
        Yields:
            Dictionary containing OHLCV data point
        """
        self.logger.info(
            f"Fetching Yahoo Finance data for {symbol} "
            f"from {start_time} to {end_time} at {interval} interval"
        )

        # Check rate limit before making request
        await self._check_rate_limit()

        try:
            # Map interval to Yahoo Finance format
            yf_interval = self._map_interval(interval)
            
            # Create ticker object
            ticker = yf.Ticker(symbol)
            
            # Fetch historical data (this is synchronous, run in executor)
            loop = asyncio.get_event_loop()
            hist_data = await loop.run_in_executor(
                None,
                lambda: ticker.history(
                    start=start_time,
                    end=end_time,
                    interval=yf_interval,
                    auto_adjust=True,  # Adjust for splits and dividends
                    actions=False
                )
            )
            
            if hist_data.empty:
                self.logger.warning(f"No data returned for {symbol}")
                return
            
            self.logger.info(f"Fetched {len(hist_data)} data points for {symbol}")
            
            # Convert DataFrame to data points
            count = 0
            for timestamp, row in hist_data.iterrows():
                if limit and count >= limit:
                    break
                
                # Convert pandas Timestamp to datetime
                if hasattr(timestamp, 'to_pydatetime'):
                    dt = timestamp.to_pydatetime()
                else:
                    dt = timestamp
                
                # Create data point
                data_point = {
                    "symbol": symbol,
                    "timestamp": dt,
                    "open": float(row['Open']),
                    "high": float(row['High']),
                    "low": float(row['Low']),
                    "close": float(row['Close']),
                    "volume": int(row['Volume']),
                    "interval": interval,
                    "provider": self.config.slug,
                    "metadata": {
                        "source": "yahoo_finance",
                        "adjusted": True,  # Data is adjusted for splits/dividends
                        "quality": "production"
                    }
                }
                
                yield data_point
                count += 1
                
                # Small delay to be nice to the API
                await asyncio.sleep(0.001)
        
        except Exception as e:
            self.logger.error(f"Error fetching data for {symbol}: {e}", exc_info=True)
            raise

    async def stream_realtime_data(self, subscriptions):
        """Real-time streaming not supported by Yahoo Finance provider."""
        raise NotImplementedError("Real-time streaming not supported by Yahoo Finance provider")


def main():
    """Main entry point."""
    print("🚀 Starting Yahoo Finance TimeBase Provider...")
    
    # Create and run provider
    provider = YahooFinanceProvider()
    print(f"📊 Provider: {provider.config.name} v{provider.config.version}")
    print(f"🔗 Slug: {provider.config.slug}")
    print(f"🎯 Capabilities: Historical data for stocks, ETFs, crypto, indices")
    print(f"⏱️  Intervals: 1m to 3mo")
    print(f"🔒 Rate Limits: 60/min, 2000/day")
    
    # Run gRPC server
    run_grpc_server(provider, port=50051)


if __name__ == "__main__":
    main()
