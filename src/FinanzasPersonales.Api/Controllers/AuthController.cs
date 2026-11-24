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
        if (!Url.IsLocalUrl(returnUrl))
            return BadRequest(new { message = "returnUrl debe ser una ruta local." });
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (HttpContext.Features.Get<Microsoft.AspNetCore.Http.Features.ISessionFeature>() != null)
        {
            HttpContext.Session.Clear();
        }

        if (Request.Headers.Accept.ToString().Contains("application/json"))
        {
            return Ok(new { mensaje = "Sesión cerrada correctamente." });
        }

        return LocalRedirect(returnUrl);
    }
}
