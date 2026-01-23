# Yahoo Finance Provider for TimeBase

A production-ready TimeBase provider that fetches real financial data from Yahoo Finance. Supports stocks, ETFs, indices, and cryptocurrencies with historical OHLCV data.

## Features

- **Real Financial Data**: Fetches actual market data from Yahoo Finance
- **Multiple Asset Types**: Stocks, ETFs, indices, cryptocurrencies
- **Flexible Intervals**: From 1-minute to 3-month intervals
- **Rate Limiting**: Built-in rate limiting (60/min, 2000/day)
- **Error Handling**: Robust error handling with retry logic
- **Adjusted Data**: Automatically adjusted for splits and dividends
- **Production Ready**: Docker container with health checks

## Supported Data Types

- **Stocks**: US and international stocks (e.g., AAPL, MSFT, TSLA)
- **ETFs**: Exchange-traded funds (e.g., SPY, QQQ, VTI)
- **Indices**: Market indices (e.g., ^GSPC, ^DJI, ^IXIC)
- **Crypto**: Cryptocurrency pairs (e.g., BTC-USD, ETH-USD)

## Supported Intervals

- Intraday: `1m`, `2m`, `5m`, `15m`, `30m`, `60m`, `90m`, `1h`
- Daily+: `1d`, `5d`, `1wk`, `1mo`, `3mo`

## Installation

### Via TimeBase API

```bash
curl -X POST http://localhost:8080/api/providers \
  -H "Content-Type: application/json" \
  -d '{"repository": "https://github.com/MarcelGa/TimeBase"}'
```

### Manual Docker Build

```bash
# Build the image
cd providers/yahoo-finance
docker build -t timebase/yahoo-finance:latest .

# Run the provider
docker run -p 50051:50051 timebase/yahoo-finance:latest
```

## Usage Examples

### Get Stock Data

```bash
# Get Apple stock daily data for 2024
curl "http://localhost:8080/api/data/AAPL?interval=1d&start=2024-01-01&end=2024-12-31"
```

### Get Crypto Data

```bash
# Get Bitcoin hourly data
curl "http://localhost:8080/api/data/BTC-USD?interval=1h&start=2024-01-01&end=2024-01-07"
```

### Get Index Data

```bash
# Get S&P 500 weekly data
curl "http://localhost:8080/api/data/^GSPC?interval=1wk&start=2024-01-01&end=2024-12-31"
```

## Rate Limits

- **Per Minute**: 60 requests
- **Per Day**: 2,000 requests

The provider automatically enforces these limits and will wait if the limit is exceeded.

## Data Quality

- **Source**: Yahoo Finance (yfinance library)
- **Adjustment**: Data is automatically adjusted for stock splits and dividends
- **Reliability**: Production-grade data from Yahoo Finance
- **Lookback**: Up to 10 years of historical data

## Configuration

No additional configuration required. The provider works out of the box with default settings.

## Development

### Local Testing

```bash
# Install dependencies
pip install -r requirements.txt

# Install TimeBase SDK
cd ../../src/TimeBase.ProviderSdk
pip install -e .

# Run provider
cd ../../providers/yahoo-finance
python src/main.py
```

### Testing with Sample Data

```python
import asyncio
from datetime import datetime
from src.main import YahooFinanceProvider

async def test():
    provider = YahooFinanceProvider()
    
    async for data_point in provider.get_historical_data(
        symbol="AAPL",
        interval="1d",
        start_time=datetime(2024, 1, 1),
        end_time=datetime(2024, 1, 31)
    ):
        print(data_point)

asyncio.run(test())
```

## Limitations

- **No Real-time Data**: This provider only supports historical data
- **Rate Limits**: Subject to Yahoo Finance rate limits
- **Data Availability**: Some symbols may have limited historical data
- **Intraday Limits**: Intraday data (1m, 5m, etc.) typically limited to recent dates

## Troubleshooting

### No Data Returned

- Check if the symbol is valid (Yahoo Finance format)
- Verify the date range is within available data
- Check rate limits haven't been exceeded

### Rate Limit Errors

The provider will automatically wait and retry if rate limits are hit. If you consistently hit limits:
- Reduce request frequency
- Use longer intervals (e.g., daily instead of minutely)
- Cache results to avoid duplicate requests

## License

MIT License - see LICENSE file for details

## Contributing

Contributions welcome! Please open an issue or pull request on GitHub.

## Support

- GitHub Issues: https://github.com/MarcelGa/TimeBase/issues
- Documentation: https://timebase.dev/docs
