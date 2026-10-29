-- SecureVote local PostgreSQL setup.
-- Run as a PostgreSQL superuser:
--   psql -U postgres -f database/setup.sql
--
-- The application role is a normal login role. It owns the application and test
-- databases, but it is not a PostgreSQL superuser.
--
-- The password below is a local demonstration credential. Change it for any shared
-- or deployed environment and keep the matching value in .env.local.

\set ON_ERROR_STOP on

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'securevote_app') THEN
        CREATE ROLE securevote_app LOGIN;
    END IF;
END
$$;

ALTER ROLE securevote_app WITH LOGIN PASSWORD 'change-me-local';

SELECT 'CREATE DATABASE securevote OWNER securevote_app'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'securevote')\gexec

SELECT 'CREATE DATABASE securevote_test OWNER securevote_app'
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = 'securevote_test')\gexec

ALTER DATABASE securevote OWNER TO securevote_app;
ALTER DATABASE securevote_test OWNER TO securevote_app;

\connect securevote
ALTER SCHEMA public OWNER TO securevote_app;
GRANT ALL ON SCHEMA public TO securevote_app;

\connect securevote_test
ALTER SCHEMA public OWNER TO securevote_app;
GRANT ALL ON SCHEMA public TO securevote_app;
