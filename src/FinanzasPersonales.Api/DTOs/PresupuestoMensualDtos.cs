using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace FinanzasPersonales.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CrearPresupuestoMensualDto
{
    [JsonRequired, GuidNoVacio] public Guid CategoriaId { get; set; }
    [JsonRequired, Range(1, 9999)] public short Año { get; set; }
    [JsonRequired, Range(1, 12)] public short Mes { get; set; }
    [JsonRequired, ImporteDev3] public decimal MontoLimite { get; set; }
}

public sealed record PresupuestoMensualResponseDto(
    Guid Id, Guid UsuarioId, Guid CategoriaId, short Año, short Mes, decimal MontoLimite)
{
    public static readonly Expression<Func<PresupuestoMensual, PresupuestoMensualResponseDto>> Proyeccion =
        x => new(x.Id, x.UsuarioId, x.CategoriaId, x.Año, x.Mes, x.MontoLimite);
}
