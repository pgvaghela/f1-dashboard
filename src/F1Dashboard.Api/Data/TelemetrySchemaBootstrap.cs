using Microsoft.EntityFrameworkCore;

namespace F1Dashboard.Api.Data;

/// <summary>
/// Ensures FastF1 telemetry tables exist on databases created before those entities
/// were added. <see cref="DatabaseFacade.EnsureCreated"/> is a no-op for existing DBs.
/// </summary>
public static class TelemetrySchemaBootstrap
{
    // Kept in sync with tools/ingest_telemetry.py SCHEMA_SQL.
    private const string SchemaSql = """
        CREATE TABLE IF NOT EXISTS telemetry_races (
            id                 integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            season             integer NOT NULL,
            round              integer NOT NULL,
            event_name         text    NOT NULL,
            circuit_short_name text    NOT NULL,
            outline_path       text    NOT NULL,
            view_width         double precision NOT NULL,
            view_height        double precision NOT NULL,
            UNIQUE (season, round)
        );

        CREATE TABLE IF NOT EXISTS telemetry_drivers (
            id                integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            telemetry_race_id integer NOT NULL REFERENCES telemetry_races(id) ON DELETE CASCADE,
            code              text NOT NULL,
            full_name         text NOT NULL,
            team_name         text NOT NULL,
            team_color        text NOT NULL,
            UNIQUE (telemetry_race_id, code)
        );

        CREATE TABLE IF NOT EXISTS telemetry_laps (
            id                  integer GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            telemetry_driver_id integer NOT NULL REFERENCES telemetry_drivers(id) ON DELETE CASCADE,
            lap_number          integer NOT NULL,
            lap_time_seconds    double precision,
            compound            text NOT NULL DEFAULT '',
            sector1_seconds     double precision,
            sector2_seconds     double precision,
            sector3_seconds     double precision,
            sector1_color       text NOT NULL DEFAULT 'yellow',
            sector2_color       text NOT NULL DEFAULT 'yellow',
            sector3_color       text NOT NULL DEFAULT 'yellow',
            UNIQUE (telemetry_driver_id, lap_number)
        );

        CREATE TABLE IF NOT EXISTS telemetry_samples (
            id               bigint GENERATED ALWAYS AS IDENTITY PRIMARY KEY,
            telemetry_lap_id integer NOT NULL REFERENCES telemetry_laps(id) ON DELETE CASCADE,
            t                double precision NOT NULL,
            x                double precision NOT NULL,
            y                double precision NOT NULL,
            speed            double precision NOT NULL,
            compound         text NOT NULL DEFAULT '',
            position         integer
        );

        CREATE INDEX IF NOT EXISTS ix_telemetry_samples_lap ON telemetry_samples(telemetry_lap_id);

        ALTER TABLE telemetry_races ADD COLUMN IF NOT EXISTS sector1_path text NOT NULL DEFAULT '';
        ALTER TABLE telemetry_races ADD COLUMN IF NOT EXISTS sector2_path text NOT NULL DEFAULT '';
        ALTER TABLE telemetry_races ADD COLUMN IF NOT EXISTS sector3_path text NOT NULL DEFAULT '';
        ALTER TABLE telemetry_drivers ADD COLUMN IF NOT EXISTS headshot_url text NOT NULL DEFAULT '';
        """;

    public static async Task EnsureAsync(F1DbContext db, ILogger logger, CancellationToken ct = default)
    {
        try
        {
            await db.Database.ExecuteSqlRawAsync(SchemaSql, ct);
            logger.LogInformation("Telemetry schema is present.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to ensure telemetry schema.");
            throw;
        }
    }
}
