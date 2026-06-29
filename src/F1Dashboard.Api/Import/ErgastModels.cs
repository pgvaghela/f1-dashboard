namespace F1Dashboard.Api.Import;

// Deserialization models for the Jolpica/Ergast F1 API (api.jolpi.ca).
// Ergast returns all numeric values as strings, so these are parsed in the importer.
// Matched case-insensitively, so the JSON's mixed casing (MRData, circuitId, ...) is fine.

public class ErgastResponse
{
    public ErgastMrData MRData { get; set; } = new();
}

public class ErgastMrData
{
    public string Total { get; set; } = "0";
    public RaceTable RaceTable { get; set; } = new();
}

public class RaceTable
{
    public List<ErgastRace> Races { get; set; } = new();
}

public class ErgastRace
{
    public string Season { get; set; } = string.Empty;
    public string Round { get; set; } = string.Empty;
    public string RaceName { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public ErgastCircuit Circuit { get; set; } = new();
    public List<ErgastResult> Results { get; set; } = new();
    // Sprint races share the result shape (driver, constructor, points, position).
    public List<ErgastResult> SprintResults { get; set; } = new();
    public List<ErgastQualifyingResult> QualifyingResults { get; set; } = new();
}

public class ErgastCircuit
{
    public string CircuitId { get; set; } = string.Empty;
    public string CircuitName { get; set; } = string.Empty;
    public ErgastLocation Location { get; set; } = new();
}

public class ErgastLocation
{
    public string Locality { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class ErgastResult
{
    public string Position { get; set; } = string.Empty;
    public string PositionText { get; set; } = string.Empty;
    public string Points { get; set; } = "0";
    public string Grid { get; set; } = "0";
    public string Laps { get; set; } = "0";
    public string Status { get; set; } = string.Empty;
    public ErgastDriver Driver { get; set; } = new();
    public ErgastConstructor Constructor { get; set; } = new();
    public ErgastTime? Time { get; set; }
    public ErgastFastestLap? FastestLap { get; set; }
}

public class ErgastDriver
{
    public string DriverId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string GivenName { get; set; } = string.Empty;
    public string FamilyName { get; set; } = string.Empty;
    public string DateOfBirth { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
}

public class ErgastConstructor
{
    public string ConstructorId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
}

public class ErgastQualifyingResult
{
    public string Position { get; set; } = string.Empty;
    public ErgastDriver Driver { get; set; } = new();
    public ErgastConstructor Constructor { get; set; } = new();
    public string? Q1 { get; set; }
    public string? Q2 { get; set; }
    public string? Q3 { get; set; }
}

public class ErgastTime
{
    public string Millis { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
}

public class ErgastFastestLap
{
    public string Lap { get; set; } = string.Empty;
    public ErgastTime Time { get; set; } = new();
}
