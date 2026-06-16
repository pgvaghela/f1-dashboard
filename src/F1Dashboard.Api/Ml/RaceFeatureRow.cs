namespace F1Dashboard.Api.Ml;

/// <summary>
/// One training/scoring example: a single driver going into a single race.
/// Every feature is computed only from results that happened strictly before the
/// target race, so the row never sees the outcome it is trying to predict.
/// </summary>
public class RaceFeatureRow
{
    /// <summary>Grid (starting) position. Pit-lane starts (grid 0) are mapped to 21.</summary>
    public float GridPosition { get; set; }

    /// <summary>1 if starting from pole, else 0.</summary>
    public float IsPole { get; set; }

    /// <summary>Driver's average finishing position over their last 5 prior races (DNF = 20).</summary>
    public float DriverRecentFinish { get; set; }

    /// <summary>Fraction of the driver's prior races (in the dataset) that they won.</summary>
    public float DriverWinRate { get; set; }

    /// <summary>Driver's average championship points per race so far this season.</summary>
    public float DriverSeasonPointsPerRace { get; set; }

    /// <summary>Constructor's average finishing position over its last 5 prior results (DNF = 20).</summary>
    public float ConstructorRecentFinish { get; set; }

    /// <summary>Driver's average finishing position at this circuit historically (falls back to recent form).</summary>
    public float DriverCircuitAvgFinish { get; set; }

    /// <summary>Label: did this driver win the race?</summary>
    public bool Won { get; set; }
}
