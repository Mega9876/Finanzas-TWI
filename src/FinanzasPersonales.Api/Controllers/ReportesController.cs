using Microsoft.AspNetCore.Authorization;
using FinanzasPersonales.Api.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FinanzasPersonales.Api;
using System.Security.Claims;

namespace MyApp.Namespace
{
    [ApiController]
[Route("api/reportes")]
[Authorize]
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
        var usuarioId = ObtenerUsuarioIdDesdeClaims();
        if (usuarioId is null)
            return Unauthorized(new { mensaje = "Sesión sin identificador de usuario válido." });

        var reporte = await _reportesService.ObtenerCuentasYFlujoAsync(usuarioId.Value, ct);
        if (reporte is null)
            return Forbid();

        return Ok(reporte);
    }


    private Guid? ObtenerUsuarioIdDesdeClaims()
    {
        var valor = User.FindFirstValue("usuario_id")
                 ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

        return Guid.TryParse(valor, out var guid) ? guid : null;
    }
}
}
