namespace F1Dashboard.Api.DTOs;

public class ConstructorStandingDto
{
    public int Position { get; set; }
    public int ConstructorId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public decimal TotalPoints { get; set; }
}
