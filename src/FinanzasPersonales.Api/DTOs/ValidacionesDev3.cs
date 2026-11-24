using System.ComponentModel.DataAnnotations;

namespace FinanzasPersonales.Api.DTOs;

[AttributeUsage(AttributeTargets.Property)]
public sealed class GuidNoVacioAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is Guid id && id != Guid.Empty;
    public override string FormatErrorMessage(string name) => $"{name} debe ser un Guid distinto de vacío.";
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class ImporteDev3Attribute(bool permiteCero = true) : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is decimal monto &&
        monto >= (permiteCero ? 0m : 0.01m) && monto <= 999999999999.99m && decimal.Round(monto, 2) == monto;
    public override string FormatErrorMessage(string name) =>
        $"{name} debe ser {(permiteCero ? "mayor o igual a cero" : "mayor que cero")}, con hasta 12 enteros y 2 decimales.";
}

[AttributeUsage(AttributeTargets.Property)]
public sealed class FechaDev3Attribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is null || value is DateOnly fecha && fecha != default;
    public override string FormatErrorMessage(string name) => $"{name} debe ser una fecha válida distinta de 0001-01-01.";
}
