namespace F1Dashboard.Api.DTOs;

public class DriverDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
}