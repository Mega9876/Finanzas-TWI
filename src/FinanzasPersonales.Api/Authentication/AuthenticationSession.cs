using System.Security.Claims;

namespace FinanzasPersonales.Api.Authentication;

public sealed record AuthenticationSession(bool IsAuthenticated, Guid? UsuarioId,
    string? IdentificadorExterno, string? Nombre, string? Email, string? Proveedor)
{
    public static AuthenticationSession From(ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true) return new(false, null, null, null, null, null);
        var local = Guid.TryParse(user.FindFirstValue("usuario_id"), out var id) ? (Guid?)id : null;
        return new(true, local, user.FindFirstValue(ClaimTypes.NameIdentifier), user.Identity.Name,
            user.FindFirstValue(ClaimTypes.Email), user.FindFirstValue(ExternalAuthentication.ProviderClaim) ?? user.FindFirstValue("provider"));
    }
}
