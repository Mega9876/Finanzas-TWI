using FinanzasPersonales.Api.Authentication;
using FinanzasPersonales.Api.Data;
using FinanzasPersonales.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanzasPersonales.Api.Controllers;

[ApiController]
[Route("api/presupuestos-mensuales")]
[Authorize(AuthenticationSchemes = Dev3Authentication.LocalScheme)]
[ServiceFilter(typeof(AccesoDev3Filter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class PresupuestosMensualesController(FinanzasDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<PresupuestoMensualResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        return Ok(await db.PresupuestosMensuales.AsNoTracking().Where(x => x.UsuarioId == usuarioId)
            .OrderByDescending(x => x.Año).ThenByDescending(x => x.Mes).ThenBy(x => x.Id)
            .Select(PresupuestoMensualResponseDto.Proyeccion).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<PresupuestoMensualResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        var result = await db.PresupuestosMensuales.AsNoTracking()
            .Where(x => x.Id == id && x.UsuarioId == usuarioId)
            .Select(PresupuestoMensualResponseDto.Proyeccion).SingleOrDefaultAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<PresupuestoMensualResponseDto>> Create(
        CrearPresupuestoMensualDto dto, CancellationToken cancellationToken,
        [FromHeader(Name = "RequestVerificationToken")] string? antiforgeryToken = null)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        if (!await db.Categorias.AsNoTracking().AnyAsync(x => x.Id == dto.CategoriaId &&
            x.UsuarioId == usuarioId && x.Activa && x.Tipo == "gasto", cancellationToken))
            return BadRequest(new { message = "La categoría debe ser propia, activa y de tipo gasto." });

        if (await db.PresupuestosMensuales.AsNoTracking().AnyAsync(x => x.UsuarioId == usuarioId &&
            x.CategoriaId == dto.CategoriaId && x.Año == dto.Año && x.Mes == dto.Mes, cancellationToken))
            return Conflict(new { message = "Ya existe un presupuesto para esa categoría y período." });

        var entity = new PresupuestoMensual
        {
            Id = Guid.NewGuid(), UsuarioId = usuarioId, CategoriaId = dto.CategoriaId,
            Año = dto.Año, Mes = dto.Mes, MontoLimite = dto.MontoLimite
        };
        db.PresupuestosMensuales.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new PresupuestoMensualResponseDto(
            entity.Id, usuarioId, entity.CategoriaId, entity.Año, entity.Mes, entity.MontoLimite));
    }
}
