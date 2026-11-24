using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using System.Text.Json.Serialization;

namespace FinanzasPersonales.Api.DTOs;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class CrearMovimientoRecurrenteDto : IValidatableObject
{
    [JsonRequired, GuidNoVacio] public Guid CuentaId { get; set; }
    [JsonRequired, GuidNoVacio] public Guid CategoriaId { get; set; }
    [Required, RegularExpression("^(ingreso|gasto)$")] public string Tipo { get; set; } = string.Empty;
    [JsonRequired, ImporteDev3(false)] public decimal MontoEstimado { get; set; }
    [Required, StringLength(200)] public string Concepto { get; set; } = string.Empty;
    [Required, RegularExpression("^(semanal|quincenal|mensual|anual)$")]
    public string Frecuencia { get; set; } = string.Empty;
    [JsonRequired, Range(1, 28)] public short DiaEjecucion { get; set; }
    [JsonRequired, FechaDev3] public DateOnly ProximaFecha { get; set; }
    public bool Activo { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Frecuencia == "semanal" && DiaEjecucion > 7)
            yield return new ValidationResult("Para frecuencia semanal el día debe estar entre lunes=1 y domingo=7.", [nameof(DiaEjecucion)]);
    }
}

public sealed record MovimientoRecurrenteResponseDto(Guid Id, Guid UsuarioId, Guid CuentaId,
    Guid CategoriaId, string Tipo, decimal MontoEstimado, string Concepto, string Frecuencia,
    short DiaEjecucion, DateOnly ProximaFecha, bool Activo)
{
    public static readonly Expression<Func<MovimientoRecurrente, MovimientoRecurrenteResponseDto>> Proyeccion =
        x => new(x.Id, x.UsuarioId, x.CuentaId, x.CategoriaId, x.Tipo, x.MontoEstimado,
            x.Concepto, x.Frecuencia, x.DiaEjecucion, x.ProximaFecha, x.Activo);
}
