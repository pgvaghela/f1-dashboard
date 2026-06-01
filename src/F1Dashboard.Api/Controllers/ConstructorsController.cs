using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using F1Dashboard.Api.Data;
using F1Dashboard.Api.Models;
using F1Dashboard.Api.DTOs;

namespace F1Dashboard.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ConstructorsController : ControllerBase
{
    private readonly F1DbContext _context;

    public ConstructorsController(F1DbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ConstructorDto>>> GetConstructors()
    {
        var constructors = await _context.Constructors
            .OrderBy(d => d.TeamName)
            .Select(d => new ConstructorDto
            {
                Id = d.Id,
                TeamName = d.TeamName,
                Nationality = d.Nationality
            })
            .ToListAsync();

        return Ok(constructors);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ConstructorDto>> GetConstructor(int id)
    {
        var constructor = await _context.Constructors
            .Where(c => c.Id == id)
            .Select(c => new ConstructorDto
            {
                Id = c.Id,
                TeamName = c.TeamName,
                Nationality = c.Nationality
            })
            .FirstOrDefaultAsync();

        if (constructor == null)
        {
            return NotFound();
        }

        return Ok(constructor);
    }
}