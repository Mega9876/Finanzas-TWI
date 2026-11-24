using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasPersonales.Api.Controllers;

[Route("api/auth/google")]
public class GoogleAuthController(IAuthenticationSchemeProvider schemes) : ExternalAuthControllerBase(schemes)
{
    protected override string ProviderName => "Google";
    protected override string ProviderScheme => "Google";
}
