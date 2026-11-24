using System.Security.Claims;
using FinanzasPersonales.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasPersonales.Api.Controllers;

[AllowAnonymous]
[Route("auth/dev3")]
[Route("auth/login")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class Dev3SessionController : Controller
{
    [HttpGet("")]
    public IActionResult Index(string? error)
    {
        // Alias históricos: una única interfaz en /. Solo conservar errores conocidos.
        var allowedErrors = ExternalAuthentication.Providers.SelectMany(provider => new[]
        {
            provider.Name.ToLowerInvariant() + "-not-configured", provider.Name.ToLowerInvariant() + "-cancelled",
            provider.Name.ToLowerInvariant() + "-failed"
        });
        var query = error is not null && allowedErrors.Contains(error) ? "?error=" + Uri.EscapeDataString(error) : string.Empty;
        return LocalRedirect(ExternalAuthentication.LoginPath + query);
    }

    [HttpGet("session")]
    public IActionResult Session() => Ok(new
    {
        authenticated = User.Identity?.IsAuthenticated == true,
        provider = User.FindFirstValue(Dev3Authentication.ProviderClaim),
        id = User.FindFirstValue(ClaimTypes.NameIdentifier),
        name = User.Identity?.Name,
        email = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email")
    });

    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(Dev3Authentication.LocalScheme);
        if (HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>() is not null)
            HttpContext.Session.Clear();
        return LocalRedirect(ExternalAuthentication.LoginPath);
    }
}
