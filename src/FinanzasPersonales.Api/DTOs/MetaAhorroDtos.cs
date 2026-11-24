using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace FinanzasPersonales.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CrearMetaAhorroDto
{
    [GuidNoVacio] public Guid? CuentaVinculadaId { get; set; }
    [Required, StringLength(100)] public string Nombre { get; set; } = string.Empty;
    [JsonRequired, ImporteDev3(false)] public decimal MontoObjetivo { get; set; }
    [ImporteDev3] public decimal MontoAhorrado { get; set; }
    [FechaDev3] public DateOnly? FechaLimite { get; set; }
    [Required, RegularExpression("^(activa|completada|pausada)$")]
    public string Estado { get; set; } = "activa";
}

public sealed record MetaAhorroResponseDto(Guid Id, Guid UsuarioId, Guid? CuentaVinculadaId,
    string Nombre, decimal MontoObjetivo, decimal MontoAhorrado, DateOnly? FechaLimite, string Estado)
{
    public static readonly Expression<Func<MetaAhorro, MetaAhorroResponseDto>> Proyeccion =
        x => new(x.Id, x.UsuarioId, x.CuentaVinculadaId, x.Nombre, x.MontoObjetivo,
            x.MontoAhorrado, x.FechaLimite, x.Estado);
}
