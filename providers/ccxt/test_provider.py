#!/usr/bin/env python3
"""
Test script for CCXT provider
Runs tests using Binance public crypto data (24/7 availability for CI/CD).
"""

import sys
import asyncio
from datetime import datetime, timedelta, timezone
from pathlib import Path

provider_root = Path(__file__).resolve().parent
sys.path.insert(0, str(provider_root / 'src'))

from main import CcxtProvider


async def test_capabilities():
    print("Testing get_capabilities...")
    provider = CcxtProvider(config_path=str(provider_root / 'config.yaml'))

    capabilities = await provider.get_capabilities()

    assert capabilities['name'] == 'CCXT Provider'
    assert capabilities['version'] == '1.0.0'
    assert capabilities['slug'] == 'ccxt'
    assert capabilities['supports_historical'] is True
    assert capabilities['supports_realtime'] is True
    assert capabilities['supports_backfill'] is True
    assert 'crypto' in capabilities['data_types']
    assert '1d' in capabilities['intervals']

    print("OK Capabilities test passed")
    return True


async def test_historical_data_btc():
    print("\nTesting BTC historical data fetch...")
    provider = CcxtProvider(config_path=str(provider_root / 'config.yaml'))

    end_time = datetime.now(timezone.utc)
    start_time = end_time - timedelta(days=3)

    symbol = 'BINANCE:BTCUSDT'
    data_points = []
    async for point in provider.get_historical_data(
        symbol=symbol,
        interval='1d',
        start_time=start_time,
        end_time=end_time
    ):
        data_points.append(point)
        print(f"  Got data point: {point['timestamp'].date()} Close=${point['close']:.2f}")

    assert len(data_points) > 0, "Should have received at least one data point"

    point = data_points[0]
    assert point['symbol'] == symbol
    assert point['interval'] == '1d'
    assert point['provider'] == 'ccxt'
    assert point['metadata']['source'] == 'ccxt'
    assert point['metadata']['exchange'] == 'binance'

    assert point['close'] > 0, "Close price should be positive"
    assert point['open'] > 0, "Open price should be positive"
    assert point['high'] > 0, "High price should be positive"
    assert point['low'] > 0, "Low price should be positive"
    assert point['volume'] >= 0, "Volume should be non-negative"

    print(f"OK BTC historical data test passed ({len(data_points)} data points received)")
    return True


async def test_multiple_symbols():
    print("\nTesting multiple symbols...")
    provider = CcxtProvider(config_path=str(provider_root / 'config.yaml'))

    symbols = ['BINANCE:BTCUSDT', 'BINANCE:ETHUSDT']
    end_time = datetime.now(timezone.utc)
    start_time = end_time - timedelta(days=2)

    for symbol in symbols:
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
            print(f"  {symbol}: ${point['close']:.2f}")

        assert count > 0, f"Should have received data for {symbol}"

    print("OK Multiple symbols test passed")
    return True


async def test_data_structure_validation():
    print("\nTesting data structure validation...")
    provider = CcxtProvider(config_path=str(provider_root / 'config.yaml'))

    end_time = datetime.now(timezone.utc)
    start_time = end_time - timedelta(days=2)

    async for point in provider.get_historical_data(
        symbol='BINANCE:ETHUSDT',
        interval='1d',
        start_time=start_time,
        end_time=end_time,
        limit=1
    ):
        required_fields = ['symbol', 'timestamp', 'open', 'high', 'low', 'close', 'volume', 'interval', 'provider', 'metadata']
        for field in required_fields:
            assert field in point, f"Missing required field: {field}"

        assert isinstance(point['symbol'], str)
        assert isinstance(point['timestamp'], datetime)
        assert isinstance(point['open'], (int, float))
        assert isinstance(point['high'], (int, float))
        assert isinstance(point['low'], (int, float))
        assert isinstance(point['close'], (int, float))
        assert isinstance(point['volume'], (int, float))
        assert isinstance(point['interval'], str)
        assert isinstance(point['provider'], str)
        assert isinstance(point['metadata'], dict)

        assert point['high'] >= point['low']
        assert point['high'] >= point['open']
        assert point['high'] >= point['close']
        assert point['low'] <= point['open']
        assert point['low'] <= point['close']
        break

    print("OK Data structure validation test passed")
    return True


async def run_all_tests():
    print("=" * 70)
    print("CCXT Provider Test Suite (Binance Public Data)")
    print("=" * 70)

    tests = [
        test_capabilities,
        test_historical_data_btc,
        test_multiple_symbols,
        test_data_structure_validation,
    ]

    passed = 0
    failed = 0

    for test in tests:
        try:
            await test()
            passed += 1
        except Exception as exc:
            print(f"x {test.__name__} failed: {exc}")
            import traceback
            traceback.print_exc()
            failed += 1

    print("\n" + "=" * 70)
    print(f"Results: {passed} passed, {failed} failed")
    print("=" * 70)

    if failed > 0:
        print("\nNote: These tests use live exchange APIs for public data.")

    return failed == 0


if __name__ == '__main__':
    success = asyncio.run(run_all_tests())
    sys.exit(0 if success else 1)
