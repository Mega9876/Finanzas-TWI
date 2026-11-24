using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace FinanzasPersonales.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CrearRecordatorioPagoDto
{
    [GuidNoVacio] public Guid? MovimientoRecurrenteId { get; set; }
    [Required, StringLength(120)] public string Titulo { get; set; } = string.Empty;
    [ImporteDev3] public decimal? MontoEstimado { get; set; }
    [JsonRequired, FechaDev3] public DateOnly FechaVencimiento { get; set; }
    public bool Pagado { get; set; }
    [FechaDev3] public DateOnly? FechaPago { get; set; }
    public string? Notas { get; set; }
}

public sealed record RecordatorioPagoResponseDto(Guid Id, Guid UsuarioId, Guid? MovimientoRecurrenteId,
    string Titulo, decimal? MontoEstimado, DateOnly FechaVencimiento, bool Pagado, DateOnly? FechaPago, string? Notas)
{
    public static readonly Expression<Func<RecordatorioPago, RecordatorioPagoResponseDto>> Proyeccion =
        x => new(x.Id, x.UsuarioId, x.MovimientoRecurrenteId, x.Titulo, x.MontoEstimado,
            x.FechaVencimiento, x.Pagado, x.FechaPago, x.Notas);
}
