-- Development seed script for providers
-- This script is executed after the database is initialized to seed providers for local development

-- Insert Yahoo Finance provider (if not exists)
INSERT INTO providers (slug, name, version, repository_url, grpc_endpoint, enabled, capabilities)
VALUES (
    'yahoo-finance',
    'Yahoo Finance Provider',
    '1.0.0',
    'https://github.com/timebase/yahoo-finance-provider',
    'timebase-yahoo-finance:50051',
    true,
    '{
        "name": "Yahoo Finance Provider",
        "version": "1.0.0",
        "slug": "yahoo-finance",
        "supportsHistorical": true,
        "supportsRealtime": true,
        "supportsBackfill": true,
        "dataTypes": ["stocks", "etf", "index", "crypto"],
        "intervals": ["1m", "2m", "5m", "15m", "30m", "60m", "90m", "1h", "1d", "5d", "1wk", "1mo", "3mo"],
        "maxLookbackDays": 3650
    }'::jsonb
)
ON CONFLICT (slug) DO UPDATE SET
    version = EXCLUDED.version,
    capabilities = EXCLUDED.capabilities;

-- Insert Minimal Test provider (if not exists)
INSERT INTO providers (slug, name, version, repository_url, grpc_endpoint, enabled, capabilities)
VALUES (
    'minimal-provider',
    'Minimal Test Provider',
    '1.0.0',
    'local://minimal-provider',
    'timebase-minimal-provider:50051',
    true,
    '{
        "name": "Minimal Test Provider",
        "version": "1.0.0",
        "slug": "minimal-provider",
        "supportsHistorical": true,
        "supportsRealtime": false,
        "dataTypes": ["stocks"],
        "intervals": ["1d"]
    }'::jsonb
)
ON CONFLICT (slug) DO NOTHING;
