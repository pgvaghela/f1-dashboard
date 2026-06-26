using System.ComponentModel.DataAnnotations.Schema;

namespace F1Dashboard.Api.Models;
public class QualifyingResult
{
    public int Id {get; set;}
    public int RaceId {get; set;}
    public int DriverId {get; set;}
    public int ConstructorId {get; set;}
    // The snake_case convention maps "Q1Time" to "q1time"; the table uses "q1_time", so pin it.
    [Column("q1_time")]
    public decimal? Q1Time {get; set;}
    [Column("q2_time")]
    public decimal? Q2Time {get; set;}
    [Column("q3_time")]
    public decimal? Q3Time {get; set;}
    public int? Position {get; set;}
    public Race Race { get; set; } = null!;
    public Driver Driver { get; set; } = null!;
    public Constructor Constructor { get; set; } = null!;
}