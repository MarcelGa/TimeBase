"""Abstract base class for TimeBase data providers."""

import logging
from abc import ABC, abstractmethod
from typing import AsyncIterator, Optional
from datetime import datetime

from .config_loader import load_config, ProviderConfig
from .models import TimeSeriesData as TimeSeriesDataModel


class TimeBaseProvider(ABC):
    """Abstract base class for all TimeBase data providers.

    This class provides the interface that all TimeBase providers must implement.
    It handles configuration loading, logging setup, and provides common utilities.

    Example:
        class MyProvider(TimeBaseProvider):
            async def get_capabilities(self):
                return {
                    "name": "My Provider",
                    "version": "1.0.0",
                    "supports_historical": True,
                    "supports_realtime": False,
                    "data_types": ["stocks"],
                    "intervals": ["1d"],
                }

            async def get_historical_data(self, symbol, interval, start_time, end_time, limit=None):
                # Your implementation here
                yield TimeSeriesData(...)
    """

    def __init__(self, config_path: str = "config.yaml"):
        """Initialize the provider.

        Args:
            config_path: Path to the provider configuration file
        """
        self.config: ProviderConfig = load_config(config_path)
        self.logger = self._setup_logger()

    def _setup_logger(self) -> logging.Logger:
        """Setup structured logging for the provider."""
        logger = logging.getLogger(f"timebase.provider.{self.config.slug}")
        logger.setLevel(logging.INFO)

        # Avoid duplicate handlers if already configured
        if not logger.handlers:
            handler = logging.StreamHandler()
            formatter = logging.Formatter(
                '%(asctime)s - %(name)s - %(levelname)s - %(message)s'
            )
            handler.setFormatter(formatter)
            logger.addHandler(handler)

        return logger

    @abstractmethod
    async def get_capabilities(self) -> dict:
        """Return provider capabilities and supported features.

        Returns:
            dict: Provider capabilities including:
                - name: Human-readable name
                - version: Semantic version
                - slug: Unique identifier
                - supports_historical: bool
                - supports_realtime: bool
                - supports_backfill: bool (optional)
                - data_types: list of supported data types
                - intervals: list of supported intervals
                - rate_limits: dict with requests_per_minute, requests_per_day
                - max_lookback_days: int (optional)
        """
        pass

    @abstractmethod
    async def get_historical_data(
        self,
        symbol: str,
        interval: str,
        start_time: datetime,
        end_time: datetime,
        limit: Optional[int] = None
    ) -> AsyncIterator[dict]:
        """Fetch historical time series data.

        Args:
            symbol: Financial symbol (e.g., "AAPL", "BTC-USD")
            interval: Time interval (e.g., "1d", "1h")
            start_time: Start timestamp (UTC)
            end_time: End timestamp (UTC)
            limit: Optional maximum number of data points

        Yields:
            dict: OHLCV data with keys:
                - symbol: str
                - timestamp: datetime
                - open: float
                - high: float
                - low: float
                - close: float
                - volume: float
                - interval: str
                - provider: str (should be self.config.slug)
                - metadata: dict (optional extensions)

        Raises:
            NotImplementedError: If historical data is not supported
            Exception: For data source errors, rate limits, etc.
        """
        pass

    @abstractmethod
    async def stream_realtime_data(
        self,
        subscriptions: AsyncIterator[dict]
    ) -> AsyncIterator[dict]:
        """Stream real-time time series data.

        Args:
            subscriptions: Async iterator of subscription control messages:
                {
                    "action": "SUBSCRIBE" | "UNSUBSCRIBE" | "PAUSE" | "RESUME",
                    "symbol": str,
                    "interval": str,
                    "options": dict (optional)
                }

        Yields:
            dict: Real-time OHLCV data (same format as historical)

        Raises:
            NotImplementedError: If real-time streaming is not supported
        """
        pass

    async def health_check(self) -> dict:
        """Return provider health status.

        Returns:
            dict: Health status with keys:
                - status: "HEALTHY" | "DEGRADED" | "UNHEALTHY"
                - message: str
                - timestamp: datetime
                - metrics: dict (optional)
        """
        return {
            "status": "HEALTHY",
            "message": "Provider is operational",
            "timestamp": datetime.utcnow()
        }

    async def setup(self) -> None:
        """Optional setup method called once during provider initialization.

        Use this for:
        - Establishing database connections
        - Loading API credentials
        - Initializing caches
        - Setting up background tasks
        """
        pass

    async def cleanup(self) -> None:
        """Optional cleanup method called during provider shutdown.

        Use this for:
        - Closing connections
        - Saving state
        - Cleaning up resources
        """
        pass