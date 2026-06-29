namespace F1Dashboard.Api.Models;
public class RaceResult
{
    public int Id {get; set;}
    public int RaceId {get; set;}
    public int DriverId {get; set;}
    public int ConstructorId {get; set;}
    public int GridPosition {get; set;}
    public int? FinishPosition {get; set;}
    public decimal Points {get; set;}
    // Points scored in this round's sprint race (0 on non-sprint weekends). Kept
    // separate from Points so the race-results view still shows main-race points,
    // while standings count race + sprint.
    public decimal SprintPoints {get; set;}
    public int Laps {get; set;}
    public int? FastestLapNumber {get; set;}
    public decimal? FastestLapTime {get; set;}
    public decimal? GapToWinnerSeconds {get; set;}
    public string Status {get; set;} = string.Empty;
    public Race Race { get; set; } = null!;
    public Driver Driver { get; set; } = null!;
    public Constructor Constructor { get; set; } = null!;
}