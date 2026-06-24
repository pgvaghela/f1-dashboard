using System.ComponentModel.DataAnnotations.Schema;

namespace F1Dashboard.Api.Models;

/// <summary>
/// One lap by one driver. Sector colours follow F1 timing convention and are precomputed
/// during ingestion: purple = session-wide fastest sector, green = the driver's personal
/// best sector, yellow = slower than their personal best.
/// </summary>
[Table("telemetry_laps")]
public class TelemetryLap
{
    [Column("id")] public int Id { get; set; }
    [Column("telemetry_driver_id")] public int TelemetryDriverId { get; set; }
    [Column("lap_number")] public int LapNumber { get; set; }
    [Column("lap_time_seconds")] public double? LapTimeSeconds { get; set; }
    [Column("compound")] public string Compound { get; set; } = string.Empty;

    [Column("sector1_seconds")] public double? Sector1Seconds { get; set; }
    [Column("sector2_seconds")] public double? Sector2Seconds { get; set; }
    [Column("sector3_seconds")] public double? Sector3Seconds { get; set; }

    [Column("sector1_color")] public string Sector1Color { get; set; } = "yellow";
    [Column("sector2_color")] public string Sector2Color { get; set; } = "yellow";
    [Column("sector3_color")] public string Sector3Color { get; set; } = "yellow";

    public TelemetryDriver Driver { get; set; } = null!;
    public List<TelemetrySample> Samples { get; set; } = new();
}
