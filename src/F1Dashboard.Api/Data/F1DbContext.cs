using Microsoft.EntityFrameworkCore;
using F1Dashboard.Api.Models;

namespace F1Dashboard.Api.Data;

public class F1DbContext : DbContext
{
    public F1DbContext(DbContextOptions<F1DbContext> options) : base(options)
    {
    }
    public DbSet<Driver> Drivers { get; set; } = null!;
    public DbSet<Constructor> Constructors { get; set; } = null!;
    public DbSet<Circuit> Circuits { get; set; } = null!;
    public DbSet<Race> Races { get; set; } = null!;
    public DbSet<RaceResult> RaceResults { get; set; } = null!;
    public DbSet<QualifyingResult> QualifyingResults { get; set; } = null!;
    public DbSet<PitStop> PitStops { get; set; } = null!;

    // Lap-telemetry tables, populated by the FastF1 Python ingestion step (tools/ingest_telemetry.py).
    public DbSet<TelemetryRace> TelemetryRaces { get; set; } = null!;
    public DbSet<TelemetryDriver> TelemetryDrivers { get; set; } = null!;
    public DbSet<TelemetryLap> TelemetryLaps { get; set; } = null!;
    public DbSet<TelemetrySample> TelemetrySamples { get; set; } = null!;
}