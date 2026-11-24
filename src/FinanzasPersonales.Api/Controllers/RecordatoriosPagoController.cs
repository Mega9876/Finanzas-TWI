using FinanzasPersonales.Api.Authentication;
using FinanzasPersonales.Api.Data;
using FinanzasPersonales.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanzasPersonales.Api.Controllers;

[ApiController]
[Route("api/recordatorios-pago")]
[Authorize(AuthenticationSchemes = Dev3Authentication.LocalScheme)]
[ServiceFilter(typeof(AccesoDev3Filter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class RecordatoriosPagoController(FinanzasDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<RecordatorioPagoResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        return Ok(await db.RecordatoriosPago.AsNoTracking().Where(x => x.UsuarioId == usuarioId)
            .OrderBy(x => x.FechaVencimiento).ThenBy(x => x.Id)
            .Select(RecordatorioPagoResponseDto.Proyeccion).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RecordatorioPagoResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        var result = await db.RecordatoriosPago.AsNoTracking().Where(x => x.Id == id && x.UsuarioId == usuarioId)
            .Select(RecordatorioPagoResponseDto.Proyeccion).SingleOrDefaultAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<RecordatorioPagoResponseDto>> Create(
        CrearRecordatorioPagoDto dto, CancellationToken cancellationToken,
        [FromHeader(Name = "RequestVerificationToken")] string? antiforgeryToken = null)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        if (dto.MovimientoRecurrenteId.HasValue && !await db.MovimientosRecurrentes.AsNoTracking().AnyAsync(
            x => x.Id == dto.MovimientoRecurrenteId && x.UsuarioId == usuarioId, cancellationToken))
            return BadRequest(new { message = "La recurrencia no está disponible para este usuario." });

        var entity = new RecordatorioPago
        {
            Id = Guid.NewGuid(), UsuarioId = usuarioId, MovimientoRecurrenteId = dto.MovimientoRecurrenteId,
            Titulo = dto.Titulo.Trim(), MontoEstimado = dto.MontoEstimado, FechaVencimiento = dto.FechaVencimiento,
            Pagado = dto.Pagado, FechaPago = dto.FechaPago, Notas = dto.Notas
        };
        db.RecordatoriosPago.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new RecordatorioPagoResponseDto(
            entity.Id, usuarioId, entity.MovimientoRecurrenteId, entity.Titulo, entity.MontoEstimado,
            entity.FechaVencimiento, entity.Pagado, entity.FechaPago, entity.Notas));
    }
}
