#!/usr/bin/env python3
"""
Test script for Yahoo Finance provider
Runs tests using cryptocurrency data (24/7 availability for CI/CD).
"""

import sys
import asyncio
from datetime import datetime, timedelta, timezone

# Add the provider source to the path
sys.path.insert(0, 'src')

from main import YahooFinanceProvider


async def test_capabilities():
    """Test the get_capabilities method."""
    print("Testing get_capabilities...")
    provider = YahooFinanceProvider()
    
    capabilities = await provider.get_capabilities()
    
    assert capabilities['name'] == 'Yahoo Finance Provider'
    assert capabilities['version'] == '1.0.0'
    assert capabilities['slug'] == 'yahoo-finance'
    assert capabilities['supports_historical'] == True
    assert capabilities['supports_realtime'] == True
    assert capabilities['supports_backfill'] == True
    assert 'crypto' in capabilities['data_types']
    assert '1d' in capabilities['intervals']
    
    print("✓ Capabilities test passed")
    print(f"  Provider: {capabilities['name']} v{capabilities['version']}")
    print(f"  Real-time: {capabilities['supports_realtime']}")
    print(f"  Intervals: {len(capabilities['intervals'])} supported")
    return True


async def test_historical_data_btc():
    """Test fetching BTC historical data (24/7 availability)."""
    print("\nTesting BTC historical data fetch...")
    provider = YahooFinanceProvider()
    
    # Fetch last 3 days of BTC-USD daily data
    end_time = datetime.now(timezone.utc)
    start_time = end_time - timedelta(days=3)
    
    print(f"  Fetching BTC-USD data from {start_time.date()} to {end_time.date()}")
    
    data_points = []
    async for point in provider.get_historical_data(
        symbol='BTC-USD',
        interval='1d',
        start_time=start_time,
        end_time=end_time
    ):
        data_points.append(point)
        print(f"  Got data point: {point['timestamp'].date()} Close=${point['close']:.2f}")
    
    assert len(data_points) > 0, "Should have received at least one data point"
    
    # Verify data structure
    point = data_points[0]
    assert 'symbol' in point
    assert 'timestamp' in point
    assert 'open' in point
    assert 'high' in point
    assert 'low' in point
    assert 'close' in point
    assert 'volume' in point
    assert 'interval' in point
    assert 'provider' in point
    assert 'metadata' in point
    
    # Verify values
    assert point['symbol'] == 'BTC-USD'
    assert point['interval'] == '1d'
    assert point['provider'] == 'yahoo-finance'
    assert point['metadata']['source'] == 'yfinance'
    
    # Verify data makes sense (BTC price should be > 0)
    assert point['close'] > 0, "Close price should be positive"
    assert point['open'] > 0, "Open price should be positive"
    assert point['high'] > 0, "High price should be positive"
    assert point['low'] > 0, "Low price should be positive"
    assert point['volume'] >= 0, "Volume should be non-negative"
    
    print(f"✓ BTC historical data test passed ({len(data_points)} data points received)")
    return True


async def test_multiple_crypto_symbols():
    """Test fetching data for multiple cryptocurrency symbols (24/7 availability)."""
    print("\nTesting multiple crypto symbols...")
    provider = YahooFinanceProvider()
    
    # Use crypto symbols that trade 24/7
    symbols = ['BTC-USD', 'ETH-USD']
    end_time = datetime.now(timezone.utc)
    start_time = end_time - timedelta(days=2)
    
    for symbol in symbols:
        print(f"  Fetching {symbol}...")
        count = 0
        async for point in provider.get_historical_data(
            symbol=symbol,
            interval='1d',
            start_time=start_time,
            end_time=end_time,
            limit=1
        ):
            count += 1
            assert point['close'] > 0, f"{symbol} price should be positive"
            print(f"    {symbol}: ${point['close']:.2f}")
        
        assert count > 0, f"Should have received data for {symbol}"
    
    print("✓ Multiple crypto symbols test passed")
    return True


async def test_different_intervals_crypto():
    """Test fetching crypto data with different intervals (24/7 availability)."""
    print("\nTesting different intervals with crypto...")
    provider = YahooFinanceProvider()
    
    # Test common intervals with BTC
    intervals = ['1d', '1wk']
    end_time = datetime.now(timezone.utc)
    start_time = end_time - timedelta(days=30)
    
    for interval in intervals:
        print(f"  Testing interval: {interval}")
        count = 0
        async for point in provider.get_historical_data(
            symbol='BTC-USD',
            interval=interval,
            start_time=start_time,
            end_time=end_time,
            limit=5
        ):
            count += 1
            assert point['close'] > 0, f"Price should be positive for interval {interval}"
        
        print(f"    Got {count} data points")
        assert count > 0, f"Should have received data for interval {interval}"
    
    print("✓ Different intervals test passed")
    return True


async def test_data_structure_validation():
    """Test that data structure is consistent and valid."""
    print("\nTesting data structure validation...")
    provider = YahooFinanceProvider()
    
    end_time = datetime.now(timezone.utc)
    start_time = end_time - timedelta(days=2)
    
    print("  Validating ETH-USD data structure...")
    async for point in provider.get_historical_data(
        symbol='ETH-USD',
        interval='1d',
        start_time=start_time,
        end_time=end_time,
        limit=1
    ):
        # Required fields
        required_fields = ['symbol', 'timestamp', 'open', 'high', 'low', 'close', 'volume', 'interval', 'provider', 'metadata']
        for field in required_fields:
            assert field in point, f"Missing required field: {field}"
        
        # Type validation
        assert isinstance(point['symbol'], str), "Symbol should be string"
        assert isinstance(point['timestamp'], datetime), "Timestamp should be datetime"
        assert isinstance(point['open'], (int, float)), "Open should be numeric"
        assert isinstance(point['high'], (int, float)), "High should be numeric"
        assert isinstance(point['low'], (int, float)), "Low should be numeric"
        assert isinstance(point['close'], (int, float)), "Close should be numeric"
        assert isinstance(point['volume'], (int, float)), "Volume should be numeric"
        assert isinstance(point['interval'], str), "Interval should be string"
        assert isinstance(point['provider'], str), "Provider should be string"
        assert isinstance(point['metadata'], dict), "Metadata should be dict"
        
        # OHLC relationship validation
        assert point['high'] >= point['low'], "High should be >= Low"
        assert point['high'] >= point['open'], "High should be >= Open"
        assert point['high'] >= point['close'], "High should be >= Close"
        assert point['low'] <= point['open'], "Low should be <= Open"
        assert point['low'] <= point['close'], "Low should be <= Close"
        
        print(f"    ✓ All fields present and valid")
        print(f"    ✓ OHLC relationships correct (H:{point['high']:.2f} >= L:{point['low']:.2f})")
        break
    
    print("✓ Data structure validation test passed")
    return True


async def run_all_tests():
    """Run all tests using crypto data (24/7 availability for CI)."""
    print("=" * 70)
    print("Yahoo Finance Provider Test Suite (Crypto Data - 24/7 Available)")
    print("=" * 70)
    
    tests = [
        test_capabilities,
        test_historical_data_btc,
        test_multiple_crypto_symbols,
        test_different_intervals_crypto,
        test_data_structure_validation,
    ]
    
    passed = 0
    failed = 0
    
    for test in tests:
        try:
            await test()
            passed += 1
        except Exception as e:
            print(f"✗ {test.__name__} failed: {e}")
            import traceback
            traceback.print_exc()
            failed += 1
    
    print("\n" + "=" * 70)
    print(f"Results: {passed} passed, {failed} failed")
    print("=" * 70)
    
    if failed > 0:
        print("\nNote: These tests use live Yahoo Finance API for crypto data.")
        print("Failures may be due to network issues or Yahoo API problems.")
    
    return failed == 0


if __name__ == '__main__':
    success = asyncio.run(run_all_tests())
    sys.exit(0 if success else 1)
