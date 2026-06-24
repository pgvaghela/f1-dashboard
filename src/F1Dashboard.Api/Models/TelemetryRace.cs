using System.ComponentModel.DataAnnotations.Schema;

namespace F1Dashboard.Api.Models;

/// <summary>
/// A FastF1 session (one Grand Prix) that has telemetry ingested. Standalone from the
/// Ergast-backed <see cref="Race"/> tables — populated only by the Python ingestion step.
/// Column names are pinned so they match the table the ingestion script creates.
/// </summary>
[Table("telemetry_races")]
public class TelemetryRace
{
    [Column("id")] public int Id { get; set; }
    [Column("season")] public int Season { get; set; }
    [Column("round")] public int Round { get; set; }
    [Column("event_name")] public string EventName { get; set; } = string.Empty;
    [Column("circuit_short_name")] public string CircuitShortName { get; set; } = string.Empty;

    /// <summary>SVG path of the circuit outline, in the same normalized space as the samples.</summary>
    [Column("outline_path")] public string OutlinePath { get; set; } = string.Empty;
    [Column("view_width")] public double ViewWidth { get; set; }
    [Column("view_height")] public double ViewHeight { get; set; }

    // The outline split into its three physical sectors (by the reference lap's boundaries),
    // so the frontend can colour each segment by that lap's sector performance.
    [Column("sector1_path")] public string Sector1Path { get; set; } = string.Empty;
    [Column("sector2_path")] public string Sector2Path { get; set; } = string.Empty;
    [Column("sector3_path")] public string Sector3Path { get; set; } = string.Empty;

    public List<TelemetryDriver> Drivers { get; set; } = new();
}
