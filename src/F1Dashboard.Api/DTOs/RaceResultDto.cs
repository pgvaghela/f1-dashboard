namespace F1Dashboard.Api.DTOs;

public class RaceResultDto
{
    public int Id { get; set; }
    public int RaceId { get; set; }
    public int DriverId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public int ConstructorId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public int GridPosition { get; set; }
    public int? FinishPosition { get; set; }
    public decimal Points { get; set; }
    public int Laps { get; set; }
    public decimal? FastestLapTime { get; set; }
    public decimal? GapToWinnerSeconds { get; set; }
    public string Status { get; set; } = string.Empty;
}
