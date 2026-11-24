using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasPersonales.Api.Controllers;

[Route("api/auth/discord")]
public class DiscordAuthController(IAuthenticationSchemeProvider schemes) : ExternalAuthControllerBase(schemes)
{
    protected override string ProviderName => "Discord";
    protected override string ProviderScheme => "Discord";
}
