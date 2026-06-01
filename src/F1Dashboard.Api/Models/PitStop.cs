namespace F1Dashboard.Api.Models;
public class PitStop
{
    public int Id {get; set;}
    public int RaceId {get; set;}
    public int DriverId {get; set;}
    public int StopNumber {get; set;}
    public int LapNumber {get; set;}
    public decimal DurationSeconds {get; set;}
    public string TireIn {get; set;} = string.Empty;
    public string TireOut {get; set;} = string.Empty;
    public Race Race { get; set; } = null!;
    public Driver Driver { get; set; } = null!;

}