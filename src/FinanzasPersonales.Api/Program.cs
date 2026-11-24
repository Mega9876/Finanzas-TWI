using DotNetEnv;
using FinanzasPersonales.Api;
using FinanzasPersonales.Api.Authentication;
using FinanzasPersonales.Api.Data;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

// Encontrar .env también al ejecutar desde src/; respetar variables ya exportadas.
Env.NoClobber().TraversePath().Load();

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

builder.Services.AddControllersWithViews();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddExternalAuthentication(builder.Configuration);
builder.Services.AddAuthorization();
builder.Services.AddOptions<VinculosIdentidadDev3Options>()
    .Bind(builder.Configuration.GetSection("Dev3"))
    .Validate(options => options.EsValida(), "Los vínculos de identidad Dev3 deben ser explícitos, válidos y no duplicados.")
    .ValidateOnStart();
builder.Services.AddScoped<AccesoDev3Filter>();

builder.Services.AddDbContext<FinanzasDbContext>(options => options.UseNpgsql(connectionString));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddHttpClient();

builder.Services.AddScoped<UsuarioService>();
builder.Services.AddScoped<CategoriaService>();
builder.Services.AddScoped<CuentaService>();
builder.Services.AddScoped<MovimientoService>();
// Los servicios antiguos de OAuth manual se conservan en disco, pero los seis
// proveedores usan ahora los middleware comunes; no se vinculan usuarios por email.

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
