using FinanzasPersonales.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace FinanzasPersonales.Api.Controllers;

[Route("api/auth/dropbox")]
[Route("auth/dropbox")]
public class DropboxAuthController(IAuthenticationSchemeProvider schemes) : ExternalAuthControllerBase(schemes)
{
    protected override string ProviderName => "Dropbox";
    protected override string ProviderScheme => Dev3Authentication.DropboxScheme;
}
