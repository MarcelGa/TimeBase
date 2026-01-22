"""Data models for TimeBase providers."""

from dataclasses import dataclass
from datetime import datetime
from typing import Dict, Any, Optional


@dataclass
class TimeSeriesData:
    """OHLCV time series data point.

    This represents a single data point in a time series with optional metadata.
    All providers must return data in this format.
    """
    symbol: str
    timestamp: datetime
    open: float
    high: float
    low: float
    close: float
    volume: float
    interval: str
    provider: str
    metadata: Optional[Dict[str, Any]] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary format for JSON serialization."""
        result = {
            "symbol": self.symbol,
            "timestamp": self.timestamp.isoformat(),
            "open": self.open,
            "high": self.high,
            "low": self.low,
            "close": self.close,
            "volume": self.volume,
            "interval": self.interval,
            "provider": self.provider,
        }

        if self.metadata:
            result["metadata"] = self.metadata

        return result

    @classmethod
    def from_dict(cls, data: Dict[str, Any]) -> 'TimeSeriesData':
        """Create from dictionary (e.g., from JSON)."""
        # Handle timestamp conversion
        if isinstance(data["timestamp"], str):
            timestamp = datetime.fromisoformat(data["timestamp"].replace('Z', '+00:00'))
        else:
            timestamp = data["timestamp"]

        return cls(
            symbol=data["symbol"],
            timestamp=timestamp,
            open=float(data["open"]),
            high=float(data["high"]),
            low=float(data["low"]),
            close=float(data["close"]),
            volume=float(data["volume"]),
            interval=data["interval"],
            provider=data["provider"],
            metadata=data.get("metadata")
        )

    def validate(self) -> None:
        """Validate the data point.

        Raises:
            ValueError: If the data point is invalid
        """
        if not self.symbol:
            raise ValueError("Symbol is required")

        if self.timestamp.tzinfo is None:
            raise ValueError("Timestamp must be timezone-aware")

        if self.open < 0 or self.high < 0 or self.low < 0 or self.close < 0:
            raise ValueError("OHLC values must be non-negative")

        if self.high < self.low:
            raise ValueError("High must be >= low")

        if self.open < self.low or self.open > self.high:
            raise ValueError("Open must be between low and high")

        if self.close < self.low or self.close > self.high:
            raise ValueError("Close must be between low and high")

        if self.volume < 0:
            raise ValueError("Volume must be non-negative")

        valid_intervals = ["1m", "5m", "15m", "30m", "1h", "4h", "1d", "1wk", "1mo"]
        if self.interval not in valid_intervals:
            raise ValueError(f"Invalid interval: {self.interval}")


@dataclass
class ProviderCapabilitiesResponse:
    """Response from get_capabilities method."""
    name: str
    version: str
    slug: str
    supports_historical: bool
    supports_realtime: bool
    supports_backfill: bool
    data_types: list[str]
    intervals: list[str]
    rate_limits: Dict[str, int]
    max_lookback_days: Optional[int] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary."""
        result = {
            "name": self.name,
            "version": self.version,
            "slug": self.slug,
            "supports_historical": self.supports_historical,
            "supports_realtime": self.supports_realtime,
            "supports_backfill": self.supports_backfill,
            "data_types": self.data_types,
            "intervals": self.intervals,
            "rate_limits": self.rate_limits,
        }

        if self.max_lookback_days:
            result["max_lookback_days"] = self.max_lookback_days

        return result


@dataclass
class HealthStatus:
    """Provider health status."""
    status: str  # "HEALTHY", "DEGRADED", "UNHEALTHY"
    message: str
    timestamp: datetime
    metrics: Optional[Dict[str, Any]] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary."""
        result = {
            "status": self.status,
            "message": self.message,
            "timestamp": self.timestamp.isoformat(),
        }

        if self.metrics:
            result["metrics"] = self.metrics

        return result


@dataclass
class StreamControl:
    """Control message for real-time streaming."""
    action: str  # "SUBSCRIBE", "UNSUBSCRIBE", "PAUSE", "RESUME"
    symbol: str
    interval: str
    options: Optional[Dict[str, Any]] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary."""
        result = {
            "action": self.action,
            "symbol": self.symbol,
            "interval": self.interval,
        }

        if self.options:
            result["options"] = self.options

        return result


@dataclass
class ErrorInfo:
    """Error information for failed operations."""
    code: str
    message: str
    details: Optional[str] = None
    retry_after_seconds: Optional[int] = None
    retry_suggestion: Optional[str] = None

    def to_dict(self) -> Dict[str, Any]:
        """Convert to dictionary."""
        result = {
            "code": self.code,
            "message": self.message,
        }

        if self.details:
            result["details"] = self.details
        if self.retry_after_seconds:
            result["retry_after_seconds"] = self.retry_after_seconds
        if self.retry_suggestion:
            result["retry_suggestion"] = self.retry_suggestion

        return result