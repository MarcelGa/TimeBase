# Yahoo Finance Provider Test Action

This composite GitHub Action runs integration tests for the Yahoo Finance provider.

## Usage

```yaml
- name: Run Yahoo Finance tests
  uses: ./providers/yahoo-finance/.github/actions/test
  with:
    python-version: '3.11'  # Optional, defaults to 3.11
```

## What It Does

1. Sets up Python environment
2. Installs provider dependencies from `requirements.txt`
3. Installs test dependencies (`pytest`, `pytest-asyncio`)
4. Runs `test_provider.py` integration tests

## Tests Included

- **test_capabilities**: Validates provider metadata
- **test_historical_data_btc**: BTC historical data fetch
- **test_multiple_crypto_symbols**: Multiple crypto symbols (BTC-USD, ETH-USD)
- **test_different_intervals_crypto**: Different time intervals (1d, 1wk)
- **test_data_structure_validation**: Field types and OHLC relationships

## Test Strategy

Tests use **cryptocurrency data** (BTC-USD, ETH-USD) to ensure:
- ✅ 24/7 availability (no market hours dependency)
- ✅ Real API validation (not mocked)
- ✅ Structural validation (data types, OHLC relationships)
- ✅ CI/CD stability (tests can run anytime)

## Requirements

- Internet access (tests call Yahoo Finance API)
- No API keys required (public data)

## Notes

- Tests may fail if Yahoo Finance API is down or rate-limited
- Uses `yfinance` library which handles authentication automatically
