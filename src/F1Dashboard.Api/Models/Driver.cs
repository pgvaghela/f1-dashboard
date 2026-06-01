namespace F1Dashboard.Api.Models;
public class Driver
{
    public int Id {get; set;}
    public string FirstName {get; set;} = string.Empty;
    public string LastName {get; set;} = string.Empty;
    public DateOnly DateOfBirth {get; set;}
    public string Code {get; set;} = string.Empty;
    public string Nationality {get; set;} = string.Empty;

}