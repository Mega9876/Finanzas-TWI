using FinanzasPersonales.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasPersonales.Api.Controllers;

[Route("api/auth/microsoft")]
[Route("auth/microsoft")]
public class MicrosoftAuthController(IAuthenticationSchemeProvider schemes) : ExternalAuthControllerBase(schemes)
{
    protected override string ProviderName => "Microsoft";
    protected override string ProviderScheme => Dev3Authentication.MicrosoftScheme;
}
