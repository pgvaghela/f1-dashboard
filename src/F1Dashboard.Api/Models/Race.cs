namespace F1Dashboard.Api.Models;
public class Race
{
    public int Id {get; set;}
    public int Season {get; set;}
    public int Round {get; set;}
    public int CircuitId {get; set;}
    public string Name {get; set;} = string.Empty;
    public DateOnly Date {get; set;}
}