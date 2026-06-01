using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using F1Dashboard.Api.Data;
using F1Dashboard.Api.Models;

namespace F1Dashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DriversController : ControllerBase
{
    private readonly F1DbContext _context;

    public DriversController(F1DbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Driver>>> GetDrivers()
    {
        var drivers = await _context.Drivers.ToListAsync();
        return Ok(drivers);
    }
}