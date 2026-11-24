namespace FinanzasPersonales.Api.Authentication;

// Alias de compatibilidad para las rutas y esquemas del aporte Dev3.
// La configuración de los seis proveedores se centraliza en ExternalAuthentication.
public static class Dev3Authentication
{
    public const string LocalScheme = ExternalAuthentication.LocalScheme;
    public const string DropboxScheme = "Dev3.Dropbox";
    public const string MicrosoftScheme = "Dev3.Microsoft";
    public const string LoginPath = "/auth/dev3";
    public const string ProviderClaim = ExternalAuthentication.ProviderClaim;

}
