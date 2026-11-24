using FinanzasPersonales.Api.Authentication;
using FinanzasPersonales.Api.Data;
using FinanzasPersonales.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanzasPersonales.Api.Controllers;

[ApiController]
[Route("api/movimientos-recurrentes")]
[Authorize(AuthenticationSchemes = Dev3Authentication.LocalScheme)]
[ServiceFilter(typeof(AccesoDev3Filter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class MovimientosRecurrentesController(FinanzasDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MovimientoRecurrenteResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        return Ok(await db.MovimientosRecurrentes.AsNoTracking().Where(x => x.UsuarioId == usuarioId)
            .OrderBy(x => x.ProximaFecha).ThenBy(x => x.Id)
            .Select(MovimientoRecurrenteResponseDto.Proyeccion).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MovimientoRecurrenteResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        var result = await db.MovimientosRecurrentes.AsNoTracking().Where(x => x.Id == id && x.UsuarioId == usuarioId)
            .Select(MovimientoRecurrenteResponseDto.Proyeccion).SingleOrDefaultAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<MovimientoRecurrenteResponseDto>> Create(
        CrearMovimientoRecurrenteDto dto, CancellationToken cancellationToken,
        [FromHeader(Name = "RequestVerificationToken")] string? antiforgeryToken = null)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        if (!await db.Cuentas.AsNoTracking().AnyAsync(x => x.Id == dto.CuentaId &&
            x.UsuarioId == usuarioId && x.Activa, cancellationToken))
            return BadRequest(new { message = "La cuenta debe ser propia y activa." });
        if (!await db.Categorias.AsNoTracking().AnyAsync(x => x.Id == dto.CategoriaId &&
            x.UsuarioId == usuarioId && x.Activa && x.Tipo == dto.Tipo, cancellationToken))
            return BadRequest(new { message = "La categoría debe ser propia, activa y coincidir con el tipo." });

        var entity = new MovimientoRecurrente
        {
            Id = Guid.NewGuid(), UsuarioId = usuarioId, CuentaId = dto.CuentaId, CategoriaId = dto.CategoriaId,
            Tipo = dto.Tipo, MontoEstimado = dto.MontoEstimado, Concepto = dto.Concepto.Trim(),
            Frecuencia = dto.Frecuencia, DiaEjecucion = dto.DiaEjecucion, ProximaFecha = dto.ProximaFecha,
            Activo = dto.Activo
        };
        db.MovimientosRecurrentes.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new MovimientoRecurrenteResponseDto(
            entity.Id, usuarioId, entity.CuentaId, entity.CategoriaId, entity.Tipo, entity.MontoEstimado,
            entity.Concepto, entity.Frecuencia, entity.DiaEjecucion, entity.ProximaFecha, entity.Activo));
    }
}
