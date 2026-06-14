namespace F1Dashboard.Api.DTOs;

public class RaceDto
{
    public int Id { get; set; }
    public int Season { get; set; }
    public int Round { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly Date { get; set; }
    public int CircuitId { get; set; }
    public string CircuitName { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public string Locality { get; set; } = string.Empty;
    public decimal? LengthKm { get; set; }
}
