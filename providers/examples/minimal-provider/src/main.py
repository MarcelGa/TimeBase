#!/usr/bin/env python3
"""Minimal TimeBase Provider Example

This is a simple example provider that generates dummy OHLCV data for testing
purposes. It demonstrates the basic structure of a TimeBase provider.
"""

import asyncio
import random
from datetime import datetime, timedelta

from timebase_sdk import TimeBaseProvider, run_grpc_server


class MinimalProvider(TimeBaseProvider):
    """A minimal provider that generates dummy OHLCV data."""

    async def get_capabilities(self) -> dict:
        """Return provider capabilities."""
        return {
            "name": self.config.name,
            "version": self.config.version,
            "slug": self.config.slug,
            "supports_historical": True,
            "supports_realtime": False,
            "supports_backfill": True,
            "data_types": ["stocks"],
            "intervals": ["1d"],
            "rate_limits": {
                "requests_per_minute": 1000,
                "requests_per_day": 100000
            },
            "max_lookback_days": 3650  # 10 years
        }

    async def get_historical_data(
        self,
        symbol: str,
        interval: str,
        start_time: datetime,
        end_time: datetime,
        limit: int = None
    ):
        """Generate dummy OHLCV data for testing.

        This method generates random but realistic-looking OHLCV data
        for demonstration purposes.
        """
        self.logger.info(f"Generating dummy data for {symbol} from {start_time} to {end_time}")

        # Get base price from config (default 100.0)
        base_price = float(self.config.options.get("base_price", {}).get("default", 100.0))

        # Generate data points
        current = start_time
        count = 0

        while current <= end_time:
            if limit and count >= limit:
                break

            # Generate realistic OHLCV data
            volatility = 0.02  # 2% daily volatility
            trend = 0.001  # Slight upward trend

            # Base price with trend
            daily_base = base_price * (1 + trend * count)

            # Add random variation
            open_price = daily_base + random.uniform(-volatility, volatility) * daily_base
            high_price = open_price + abs(random.gauss(0, volatility * 0.5)) * open_price
            low_price = open_price - abs(random.gauss(0, volatility * 0.5)) * open_price
            close_price = open_price + random.gauss(0, volatility) * open_price

            # Ensure OHLC relationships
            high_price = max(high_price, open_price, close_price)
            low_price = min(low_price, open_price, close_price)

            # Generate volume (realistic range)
            volume = random.uniform(1000000, 10000000)

            # Create data point
            data_point = {
                "symbol": symbol,
                "timestamp": current,
                "open": round(open_price, 2),
                "high": round(high_price, 2),
                "low": round(low_price, 2),
                "close": round(close_price, 2),
                "volume": int(volume),
                "interval": interval,
                "provider": self.config.slug,
                "metadata": {
                    "generated": True,
                    "quality": "synthetic"
                }
            }

            yield data_point

            # Move to next day
            current += timedelta(days=1)
            count += 1

            # Small delay to simulate real API calls
            await asyncio.sleep(0.001)

    async def stream_realtime_data(self, subscriptions):
        """Real-time streaming not supported in this minimal example."""
        raise NotImplementedError("Real-time streaming not supported by minimal provider")


def main():
    """Main entry point."""
    print("🚀 Starting Minimal TimeBase Provider...")

    # Create and run provider
    provider = MinimalProvider()
    print(f"📊 Provider: {provider.config.name} v{provider.config.version}")
    print(f"🔗 Slug: {provider.config.slug}")
    print(f"🎯 Capabilities: Historical data only")

    # Run gRPC server
    run_grpc_server(provider, port=50051)


if __name__ == "__main__":
    main()