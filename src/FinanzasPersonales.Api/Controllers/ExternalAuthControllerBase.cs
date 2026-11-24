using FinanzasPersonales.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasPersonales.Api.Controllers;

[ApiController]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public abstract class ExternalAuthControllerBase(IAuthenticationSchemeProvider schemes) : ControllerBase
{
    protected abstract string ProviderName { get; }
    protected abstract string ProviderScheme { get; }

    // GET conserva los enlaces anteriores. State/correlación siempre los valida el middleware.
    [AllowAnonymous]
    [HttpGet("login")]
    public Task<IActionResult> Login([FromQuery] string? returnUrl = null) => StartLogin(returnUrl);

    [AllowAnonymous]
    [HttpPost("login")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> LoginPost([FromQuery] string? returnUrl = null) => StartLogin(returnUrl);

    private async Task<IActionResult> StartLogin(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && !Url.IsLocalUrl(returnUrl))
            return BadRequest(new { message = "returnUrl debe ser una ruta local." });
        if (await schemes.GetSchemeAsync(ProviderScheme) is null)
            return LocalRedirect(ExternalAuthentication.LoginPath + "?error=" + ProviderName.ToLowerInvariant() + "-not-configured");
        return Challenge(new AuthenticationProperties
        {
            RedirectUri = string.IsNullOrWhiteSpace(returnUrl) ? ExternalAuthentication.LoginPath : returnUrl
        }, ProviderScheme);
    }

    [Authorize(AuthenticationSchemes = ExternalAuthentication.LocalScheme)]
    [HttpGet("user")]
    public IActionResult GetUser()
    {
        if (User.FindFirst(ExternalAuthentication.ProviderClaim)?.Value != ProviderName)
            return StatusCode(403, new { message = "La sesión activa pertenece a otro proveedor." });
        return Ok(AuthenticationSession.From(User));
    }
}
