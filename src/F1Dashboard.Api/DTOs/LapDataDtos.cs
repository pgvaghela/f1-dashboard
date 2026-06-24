namespace F1Dashboard.Api.DTOs;

public record LapRaceDto(int RaceId, int Season, int Round, string Name, string CircuitName);

public record LapDriverDto(int DriverId, string Code, string Name, string TeamName, string TeamColor, string HeadshotUrl);

public record LapListItemDto(int LapId, int LapNumber, double? LapTimeSeconds, string Compound);

/// <summary><see cref="SectorPaths"/> holds the three sector segments, aligned with the sector list.</summary>
public record TrackOutlineDto(string Path, double Width, double Height, IReadOnlyList<string> SectorPaths);

public record LapSampleDto(double T, double X, double Y, double Speed, string Compound, int? Position);

public record SectorTimeDto(int Sector, double? TimeSeconds, string Color);

public record LapDetailDto(
    int LapId,
    int LapNumber,
    double? LapTimeSeconds,
    string TeamColor,
    string Compound,
    TrackOutlineDto Outline,
    IReadOnlyList<SectorTimeDto> Sectors,
    IReadOnlyList<LapSampleDto> Samples);
