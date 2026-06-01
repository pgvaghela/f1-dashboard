using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using F1Dashboard.Api.Data;
using F1Dashboard.Api.Models;
using F1Dashboard.Api.DTOs;

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
    public async Task<ActionResult<IEnumerable<DriverDto>>> GetDrivers()
    {
        var drivers = await _context.Drivers
            .OrderBy(d => d.LastName)
            .Select(d => new DriverDto
            {
                Id = d.Id,
                FirstName = d.FirstName,
                LastName = d.LastName,
                Code = d.Code,
                Nationality = d.Nationality
            })
            .ToListAsync();

        return Ok(drivers);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<DriverDto>> GetDriver(int id)
    {
        var driver = await _context.Drivers
            .Where(d => d.Id == id)
            .Select(d => new DriverDto
            {
                Id = d.Id,
                FirstName = d.FirstName,
                LastName = d.LastName,
                Code = d.Code,
                Nationality = d.Nationality
            })
            .FirstOrDefaultAsync();

        if (driver == null)
        {
            return NotFound();
        }

        return Ok(driver);
    }
}