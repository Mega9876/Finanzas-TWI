using FinanzasPersonales.Api.Authentication;
using FinanzasPersonales.Api.Data;
using FinanzasPersonales.Api.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FinanzasPersonales.Api.Controllers;

[ApiController]
[Route("api/metas-ahorro")]
[Authorize(AuthenticationSchemes = Dev3Authentication.LocalScheme)]
[ServiceFilter(typeof(AccesoDev3Filter))]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class MetasAhorroController(FinanzasDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<MetaAhorroResponseDto>>> GetAll(CancellationToken cancellationToken)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        return Ok(await db.MetasAhorro.AsNoTracking().Where(x => x.UsuarioId == usuarioId)
            .OrderBy(x => x.Id).Select(MetaAhorroResponseDto.Proyeccion).ToListAsync(cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<MetaAhorroResponseDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        var result = await db.MetasAhorro.AsNoTracking().Where(x => x.Id == id && x.UsuarioId == usuarioId)
            .Select(MetaAhorroResponseDto.Proyeccion).SingleOrDefaultAsync(cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<ActionResult<MetaAhorroResponseDto>> Create(
        CrearMetaAhorroDto dto, CancellationToken cancellationToken,
        [FromHeader(Name = "RequestVerificationToken")] string? antiforgeryToken = null)
    {
        var usuarioId = AccesoDev3Filter.UsuarioId(HttpContext);
        if (dto.CuentaVinculadaId.HasValue && !await db.Cuentas.AsNoTracking().AnyAsync(
            x => x.Id == dto.CuentaVinculadaId && x.UsuarioId == usuarioId, cancellationToken))
            return BadRequest(new { message = "La cuenta vinculada no está disponible para este usuario." });

        var entity = new MetaAhorro
        {
            Id = Guid.NewGuid(), UsuarioId = usuarioId, CuentaVinculadaId = dto.CuentaVinculadaId,
            Nombre = dto.Nombre.Trim(), MontoObjetivo = dto.MontoObjetivo, MontoAhorrado = dto.MontoAhorrado,
            FechaLimite = dto.FechaLimite, Estado = dto.Estado
        };
        db.MetasAhorro.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = entity.Id }, new MetaAhorroResponseDto(
            entity.Id, usuarioId, entity.CuentaVinculadaId, entity.Nombre, entity.MontoObjetivo,
            entity.MontoAhorrado, entity.FechaLimite, entity.Estado));
    }
}
