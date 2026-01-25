-- TimeBase Database Initialization
-- This script sets up the TimescaleDB database for TimeBase

-- Enable TimescaleDB extension
CREATE EXTENSION IF NOT EXISTS timescaledb;

-- Create providers table
-- Stores information about installed data providers
CREATE TABLE providers (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    slug VARCHAR(100) UNIQUE NOT NULL,
    name VARCHAR(255) NOT NULL,
    version VARCHAR(50) NOT NULL,
    repository_url TEXT,
    image_url TEXT,  -- Optional Docker image URL
    enabled BOOLEAN DEFAULT true,
    config JSONB,  -- Provider-specific configuration
    capabilities JSONB,  -- Provider capabilities (nullable in MVP)
    created_at TIMESTAMPTZ DEFAULT NOW(),
    updated_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create symbols table
-- Stores information about available financial symbols
CREATE TABLE symbols (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    symbol VARCHAR(50) UNIQUE NOT NULL,
    name VARCHAR(255),
    type VARCHAR(50),  -- 'stock', 'etf', 'crypto', 'forex', 'index', etc.
    exchange VARCHAR(50),  -- 'NASDAQ', 'NYSE', 'BINANCE', etc.
    currency VARCHAR(10) DEFAULT 'USD',  -- Trading currency
    sector VARCHAR(100),  -- Industry sector
    country VARCHAR(100),  -- Country of origin
    isin VARCHAR(12),  -- International Security Identification Number
    metadata JSONB,  -- Additional symbol information
    created_at TIMESTAMPTZ DEFAULT NOW()
);

-- Create time_series_data table (hypertable)
-- Main table for storing OHLCV time series data
-- This will be converted to a TimescaleDB hypertable for optimal performance
CREATE TABLE time_series_data (
    -- Time dimension (required for hypertable)
    time TIMESTAMPTZ NOT NULL,

    -- Symbol and provider dimensions
    symbol VARCHAR(50) NOT NULL,
    provider_id UUID NOT NULL REFERENCES providers(id),

    -- Time series metadata
    interval VARCHAR(10) NOT NULL,  -- '1m', '5m', '1h', '1d', etc.

    -- OHLCV data (core financial data)
    open DOUBLE PRECISION NOT NULL,
    high DOUBLE PRECISION NOT NULL,
    low DOUBLE PRECISION NOT NULL,
    close DOUBLE PRECISION NOT NULL,
    volume DOUBLE PRECISION NOT NULL,

    -- Optional metadata (provider-specific extensions)
    metadata JSONB,

    -- Constraints
    CONSTRAINT time_series_data_pkey PRIMARY KEY (time, symbol, interval, provider_id),
    CONSTRAINT valid_ohlc CHECK (high >= low AND high >= open AND high >= close AND low <= open AND low <= close)
);

-- Convert to TimescaleDB hypertable
-- Partition by time dimension for optimal query performance
SELECT create_hypertable('time_series_data', 'time', if_not_exists => TRUE);

-- Create indexes for common query patterns
-- Symbol + time range queries (most common)
CREATE INDEX IF NOT EXISTS idx_time_series_symbol_time ON time_series_data (symbol, time DESC);

-- Interval-based queries
CREATE INDEX IF NOT EXISTS idx_time_series_interval_time ON time_series_data (interval, time DESC);

-- Provider-based queries (for data source analysis)
CREATE INDEX IF NOT EXISTS idx_time_series_provider_time ON time_series_data (provider_id, time DESC);

-- Composite index for symbol + interval queries
CREATE INDEX IF NOT EXISTS idx_time_series_symbol_interval_time ON time_series_data (symbol, interval, time DESC);

-- Compression policy
-- Compress data older than 7 days to save space
ALTER TABLE time_series_data SET (
    timescaledb.compress,
    timescaledb.compress_segmentby = 'symbol,interval,provider_id'
);
SELECT add_compression_policy('time_series_data', INTERVAL '7 days', if_not_exists => TRUE);

-- Retention policy (optional - adjust as needed)
-- Keep data for 2 years, then drop automatically
-- SELECT add_retention_policy('time_series_data', INTERVAL '2 years', if_not_exists => TRUE);

-- Continuous aggregates for performance
-- Daily OHLCV summary for faster queries
CREATE MATERIALIZED VIEW IF NOT EXISTS daily_ohlcv
WITH (timescaledb.continuous) AS
SELECT
    time_bucket('1 day', time) AS day,
    symbol,
    provider_id,
    first(open, time) AS open,
    max(high) AS high,
    min(low) AS low,
    last(close, time) AS close,
    sum(volume) AS volume,
    count(*) AS data_points
FROM time_series_data
WHERE interval = '1d'
GROUP BY day, symbol, provider_id
WITH NO DATA;

-- Enable automatic refresh for continuous aggregate
SELECT add_continuous_aggregate_policy('daily_ohlcv',
    start_offset => INTERVAL '3 days',
    end_offset => INTERVAL '1 hour',
    schedule_interval => INTERVAL '1 hour',
    if_not_exists => TRUE);

-- Create indexes on continuous aggregate
CREATE INDEX IF NOT EXISTS idx_daily_ohlcv_symbol_day ON daily_ohlcv (symbol, day DESC);
CREATE INDEX IF NOT EXISTS idx_daily_ohlcv_provider_day ON daily_ohlcv (provider_id, day DESC);

-- Insert some sample data for testing
-- This will be replaced by real data from providers
-- INSERT INTO providers (slug, name, version, image_url, capabilities) VALUES
-- ('minimal-provider', 'Minimal Provider', '1.0.0', 'timebase/minimal-provider:latest', '{
--   "supports_historical": true,
--   "supports_realtime": false,
--   "data_types": ["stocks"],
--   "intervals": ["1d"]
-- }'::jsonb);

INSERT INTO symbols (symbol, name, type, exchange) VALUES
('AAPL', 'Apple Inc.', 'stock', 'NASDAQ'),
('GOOGL', 'Alphabet Inc.', 'stock', 'NASDAQ'),
('MSFT', 'Microsoft Corporation', 'stock', 'NASDAQ'),
('TSLA', 'Tesla Inc.', 'stock', 'NASDAQ');

-- Create a view for easy querying
CREATE OR REPLACE VIEW latest_prices AS
SELECT
    symbol,
    last(close, time) as latest_price,
    max(time) as last_updated
FROM time_series_data
WHERE time >= NOW() - INTERVAL '1 day'
GROUP BY symbol;

-- Grant permissions for the application user
-- (This would be done in production with proper user management)
-- GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO timebase_app;
-- GRANT USAGE ON SCHEMA public TO timebase_app;

-- Add some helpful comments
COMMENT ON TABLE providers IS 'Installed data providers with their capabilities and configuration';
COMMENT ON TABLE symbols IS 'Available financial symbols with metadata';
COMMENT ON TABLE time_series_data IS 'OHLCV time series data stored as TimescaleDB hypertable';
COMMENT ON MATERIALIZED VIEW daily_ohlcv IS 'Pre-aggregated daily OHLCV data for performance';
COMMENT ON VIEW latest_prices IS 'Latest prices for all symbols (last 24 hours)';

-- Log successful initialization
DO $$
BEGIN
    RAISE NOTICE 'TimeBase database initialized successfully';
    RAISE NOTICE 'Hypertable created: time_series_data';
    RAISE NOTICE 'Compression policy: 7 days';
    RAISE NOTICE 'Continuous aggregate: daily_ohlcv';
END
$$;