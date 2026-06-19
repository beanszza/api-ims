using Infrastructures.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace api_scm.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuditLogsController : ControllerBase
{
    private readonly ScmDbContext _context;

    public AuditLogsController(ScmDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs([FromQuery] string type)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(a => a.EntityName.ToLower() == type.ToLower());
        }

        var logs = await query.OrderByDescending(a => a.Timestamp).ToListAsync();

        var result = logs.Select(a => new
        {
            id = a.LogId.ToString(),
            activity = a.Action,
            entityName = a.EntityId,
            timestamp = a.Timestamp.ToString("MM/dd/yyyy HH:mm"),
            user = string.IsNullOrEmpty(a.FieldName) ? "scmsuser" : a.FieldName
        });

        return Ok(result);
    }
}
