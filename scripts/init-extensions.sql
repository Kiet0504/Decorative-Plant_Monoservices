-- Enable PostgreSQL extensions for DecorativePlantDB
-- This script runs automatically when PostgreSQL container starts for the first time

-- UUID generation
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

-- Cryptographic functions
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Statistics for query performance
CREATE EXTENSION IF NOT EXISTS "pg_stat_statements";

-- Full-text search (if needed)
CREATE EXTENSION IF NOT EXISTS "pg_trgm";

-- Additional extensions can be added here as needed
