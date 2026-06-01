namespace F1Dashboard.Api.Models;
public class Circuit
{
    public int Id {get; set;}
    public string CircuitName {get; set;} = string.Empty;
    public string Country {get; set;} = string.Empty;
    public string Locality {get; set;} = string.Empty;
    public decimal? LengthKm {get; set;}
}