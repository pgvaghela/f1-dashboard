using Microsoft.AspNetCore.Mvc;
using F1Dashboard.Api.Ml;

namespace F1Dashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PredictionsController : ControllerBase
{
    private readonly RaceWinnerModel _model;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public PredictionsController(RaceWinnerModel model, IWebHostEnvironment env, IConfiguration config)
    {
        _model = model;
        _env = env;
        _config = config;
    }

    /// <summary>Races in a season the model can predict (newest round first).</summary>
    [HttpGet("races/{season:int}")]
    public async Task<ActionResult<IReadOnlyList<RaceWinnerModel.RaceInfo>>> GetRaces(int season)
    {
        var races = await _model.GetRacesAsync(season);
        if (races.Count == 0)
        {
            return NotFound($"No races available to predict for season {season}");
        }

        return Ok(races);
    }

    /// <summary>
    /// Ranked win probabilities for every driver in a race, plus the actual result
    /// for comparison. The first call in the process trains the model (a few seconds).
    /// </summary>
    [HttpGet("race/{raceId:int}")]
    public async Task<ActionResult<RaceWinnerModel.RacePredictionResult>> GetRacePrediction(int raceId)
    {
        var result = await _model.PredictRaceAsync(raceId);
        if (result is null)
        {
            return NotFound($"No race found with id {raceId}");
        }

        return Ok(result);
    }

    /// <summary>
    /// Forces the model to retrain from the current data. Gated like the importer:
    /// available in Development, or when AllowImport=true.
    /// </summary>
    [HttpPost("retrain")]
    public async Task<IActionResult> Retrain()
    {
        if (!_env.IsDevelopment() && !_config.GetValue<bool>("AllowImport"))
        {
            return NotFound();
        }

        var result = await _model.RetrainAsync();
        return Ok(result);
    }
}
