using BarberBook.Data;
using Microsoft.AspNetCore.Mvc;

namespace BarberBook.Web.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    private readonly BarberBookDbContext _dbContext;
    private readonly ILogger<HealthController> _logger;

    public HealthController(BarberBookDbContext dbContext, ILogger<HealthController> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> GetHealth(CancellationToken cancellationToken)
    {
        var dbStatus = "down";
        try
        {
            var canConnect = await _dbContext.Database.CanConnectAsync(cancellationToken);
            if (canConnect)
            {
                dbStatus = "up";
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Health check database connection failed.");
            dbStatus = "down";
        }

        var response = new
        {
            status = "ok",
            db = dbStatus
        };

        if (dbStatus == "up")
        {
            return Ok(response);
        }

        // Return 200 with db: down or 503 depending on configuration, but return the exact json shape
        return Ok(response);
    }
}

