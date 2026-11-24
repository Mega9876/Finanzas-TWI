using FinanzasPersonales.Api.Authentication;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasPersonales.Api.Controllers;

[ApiController]
[Route("api/auth")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class AuthController : ControllerBase
{
    [HttpGet("me")]
    public IActionResult GetCurrentUser()
    {
        return Ok(AuthenticationSession.From(User));
    }

    [AllowAnonymous]
    [HttpGet("csrf")]
    public IActionResult Csrf([FromServices] IAntiforgery antiforgery) =>
        Ok(new { token = antiforgery.GetAndStoreTokens(HttpContext).RequestToken });

    [AllowAnonymous]
    [HttpGet("providers")]
    public async Task<IActionResult> Providers([FromServices] IAuthenticationSchemeProvider schemes)
    {
        var registered = (await schemes.GetAllSchemesAsync()).Select(scheme => scheme.Name).ToHashSet();
        return Ok(ExternalAuthentication.Providers.Select(provider => new
        {
            name = provider.Name,
            configured = registered.Contains(provider.Scheme)
        }));
    }

    [Authorize]
    [HttpPost("logout")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout([FromQuery] string returnUrl = "/")
    {
        if (!EsUrlValida(returnUrl))
            return BadRequest(new { message = "returnUrl debe ser una ruta local o un origen permitido." });
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>() != null)
        {
            HttpContext.Session.Clear();
        }

        if (Request.Headers.Accept.ToString().Contains("application/json"))
        {
            return Ok(new { mensaje = "Sesión cerrada correctamente." });
        }

        if (Url.IsLocalUrl(returnUrl))
            return LocalRedirect(returnUrl);

        return Redirect(returnUrl);
    }

    private bool EsUrlValida(string? returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl)) return true;
        if (Url.IsLocalUrl(returnUrl)) return true;

        if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
        {
            var config = HttpContext.RequestServices.GetService<IConfiguration>();
            var rawCors = config?["CORS_ALLOWED_ORIGINS"];
            var defaultOrigins = new[] { "http://127.0.0.1:5500", "http://localhost:5500", "http://127.0.0.1:3000", "http://localhost:3000" };
            var allowedOrigins = !string.IsNullOrWhiteSpace(rawCors)
                ? rawCors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                : defaultOrigins;

            var origin = $"{uri.Scheme}://{uri.Authority}";
            return allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase);
        }

        return false;
    }
}
