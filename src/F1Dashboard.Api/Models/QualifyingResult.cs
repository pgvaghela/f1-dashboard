namespace F1Dashboard.Api.Models;
public class QualifyingResult
{
    public int Id {get; set;}
    public int RaceId {get; set;}
    public int DriverId {get; set;}
    public int ConstructorId {get; set;}
    public decimal? Q1Time {get; set;}
    public decimal? Q2Time {get; set;}
    public decimal? Q3Time {get; set;}
    public int? Position {get; set;}
    public Race Race { get; set; } = null!;
    public Driver Driver { get; set; } = null!;
    public Constructor Constructor { get; set; } = null!;
}