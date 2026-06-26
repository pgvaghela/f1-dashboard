namespace F1Dashboard.Api.DTOs;

public class DriverStandingDto
{
    public int Position { get; set; }
    public int DriverId { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public decimal TotalPoints { get; set; }
}