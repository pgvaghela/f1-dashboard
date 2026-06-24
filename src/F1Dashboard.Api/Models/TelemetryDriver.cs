using System.ComponentModel.DataAnnotations.Schema;

namespace F1Dashboard.Api.Models;

/// <summary>A driver entry for a telemetry session, with the team colour for the trace dot.</summary>
[Table("telemetry_drivers")]
public class TelemetryDriver
{
    [Column("id")] public int Id { get; set; }
    [Column("telemetry_race_id")] public int TelemetryRaceId { get; set; }
    [Column("code")] public string Code { get; set; } = string.Empty;
    [Column("full_name")] public string FullName { get; set; } = string.Empty;
    [Column("team_name")] public string TeamName { get; set; } = string.Empty;
    [Column("team_color")] public string TeamColor { get; set; } = string.Empty;
    [Column("headshot_url")] public string HeadshotUrl { get; set; } = string.Empty;

    public TelemetryRace Race { get; set; } = null!;
    public List<TelemetryLap> Laps { get; set; } = new();
}
