using System.ComponentModel.DataAnnotations.Schema;

namespace F1Dashboard.Api.Models;

/// <summary>
/// One downsampled point along a lap. (x, y) are normalized into the session's track
/// outline space so the frontend can plot the dot directly. <c>T</c> is seconds from lap start.
/// </summary>
[Table("telemetry_samples")]
public class TelemetrySample
{
    [Column("id")] public long Id { get; set; }
    [Column("telemetry_lap_id")] public int TelemetryLapId { get; set; }
    [Column("t")] public double T { get; set; }
    [Column("x")] public double X { get; set; }
    [Column("y")] public double Y { get; set; }
    [Column("speed")] public double Speed { get; set; }
    [Column("compound")] public string Compound { get; set; } = string.Empty;
    [Column("position")] public int? Position { get; set; }

    public TelemetryLap Lap { get; set; } = null!;
}
