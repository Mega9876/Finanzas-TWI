using System.Security.Claims;
using FinanzasPersonales.Api.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace FinanzasPersonales.Api.Authentication;

// Opciones del servidor, NO entidades EF ni datos recibidos por la API.
public sealed class VinculoIdentidadDev3
{
    public string Proveedor { get; set; } = string.Empty;
    public string IdentificadorExterno { get; set; } = string.Empty;
    public Guid UsuarioId { get; set; }
}

public sealed class VinculosIdentidadDev3Options
{
    public List<VinculoIdentidadDev3> VinculosIdentidad { get; set; } = [];

    public bool EsValida() => VinculosIdentidad.All(v =>
        v.Proveedor is "Dropbox" or "Microsoft" &&
        !string.IsNullOrWhiteSpace(v.IdentificadorExterno) && v.UsuarioId != Guid.Empty) &&
        VinculosIdentidad.Select(v => (v.Proveedor, v.IdentificadorExterno)).Distinct().Count() == VinculosIdentidad.Count;
}

public sealed class AccesoDev3Filter(
    FinanzasDbContext db, IOptionsSnapshot<VinculosIdentidadDev3Options> options)
    : IAsyncResourceFilter, IExceptionFilter
{
    private static readonly object UsuarioKey = new();

    public static Guid UsuarioId(HttpContext context) => (Guid)context.Items[UsuarioKey]!;

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        // [Authorize] valida la cookie antes de este filtro. No confiar en headers ni correo.
        var proveedor = user.FindFirstValue(Dev3Authentication.ProviderClaim);
        var externo = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var vinculo = options.Value.VinculosIdentidad.SingleOrDefault(v =>
            v.Proveedor == proveedor && v.IdentificadorExterno == externo);

        if (user.Identity?.IsAuthenticated != true)
        {
            context.Result = Error(401, "Se requiere una sesión autenticada.");
            return;
        }
        if (vinculo is null)
        {
            context.Result = Error(403, "La identidad externa no está vinculada a un usuario local autorizado.");
            return;
        }

        bool activo;
        try
        {
            activo = await db.Usuarios.AsNoTracking().AnyAsync(
                u => u.Id == vinculo.UsuarioId && u.Activo, context.HttpContext.RequestAborted);
        }
        catch (NpgsqlException)
        {
            context.Result = Error(503, "La base de datos no está disponible o no tiene el esquema requerido.");
            return;
        }
        if (!activo)
        {
            context.Result = Error(403, "El usuario local no está disponible o está inactivo.");
            return;
        }

        context.HttpContext.Items[UsuarioKey] = vinculo.UsuarioId;
        await next();
    }

    public void OnException(ExceptionContext context)
    {
        var postgres = context.Exception as PostgresException ?? context.Exception.InnerException as PostgresException;
        var result = postgres?.SqlState switch
        {
            PostgresErrorCodes.UniqueViolation => Error(409, "Ya existe un registro con esos datos únicos."),
            PostgresErrorCodes.ForeignKeyViolation => Error(400, "Una relación ya no está disponible."),
            PostgresErrorCodes.CheckViolation or PostgresErrorCodes.NotNullViolation or
            PostgresErrorCodes.NumericValueOutOfRange or PostgresErrorCodes.StringDataRightTruncation =>
                Error(400, "Los datos no cumplen las restricciones del registro."),
            _ => null
        };
        if (result is null && (context.Exception is NpgsqlException ||
            context.Exception is DbUpdateException || context.Exception.InnerException is NpgsqlException))
            result = Error(503, "No se pudo completar la operación en la base de datos.");

        if (result is not null)
        {
            context.Result = result;
            context.ExceptionHandled = true;
        }
    }

    private static ObjectResult Error(int status, string title) =>
        new(new ProblemDetails { Status = status, Title = title }) { StatusCode = status };
}
