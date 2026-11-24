using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasPersonales.Api.Controllers;

[ApiController]
[Route("api/reportes")]
[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class ReportesController : ControllerBase
{
    private readonly ReportesService _reportesService;

    public ReportesController(ReportesService reportesService)
    {
        _reportesService = reportesService;
    }

    [HttpGet("cuentas-flujo")]
    public async Task<IActionResult> GetCuentasYFlujo(CancellationToken ct)
    {
        var email = User.FindFirstValue(ClaimTypes.Email)
                 ?? User.FindFirstValue("email");

        if (string.IsNullOrWhiteSpace(email))
            return Unauthorized(new { mensaje = "La sesión actual no contiene un correo electrónico válido." });

        var nombre = User.Identity?.Name
                  ?? User.FindFirstValue(ClaimTypes.Name)
                  ?? User.FindFirstValue("name");

        var reporte = await _reportesService.ObtenerCuentasYFlujoPorEmailAsync(email, nombre, ct);
        if (reporte is null)
            return Forbid();

        return Ok(reporte);
    }
}