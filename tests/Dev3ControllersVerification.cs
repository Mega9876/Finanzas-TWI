// Harness de integración, NO parte de la API. No agrega rutas de acceso de prueba a producción.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FinanzasPersonales.Api;
using FinanzasPersonales.Api.Authentication;
using FinanzasPersonales.Api.Controllers;
using FinanzasPersonales.Api.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

internal static class Dev3ControllersVerification
{
    private static int assertions;
    private static void Check(bool condition, string message)
    {
        assertions++;
        if (!condition) throw new Exception(message);
    }

    public static async Task Main(string[] args)
    {
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();
        var accountA = Guid.NewGuid();
        var accountB = Guid.NewGuid();
        var categoryA = Guid.NewGuid();
        var categoryB = Guid.NewGuid();
        var incomeA = Guid.NewGuid();
        var inactiveA = Guid.NewGuid();
        const string externalA = "dropbox|fixture-a";
        const string externalB = "https://login.microsoftonline.com/fixture/v2.0|fixture-b";
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(PresupuestosMensualesController).Assembly.GetName().Name,
            ContentRootPath = System.IO.Path.Combine(args[1], "src/FinanzasPersonales.Api"),
            EnvironmentName = "Development"
        });
        builder.Logging.ClearProviders();
        // Aislar completamente los secretos y las claves de cookies del usuario/desarrollo.
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Dev3:VinculosIdentidad:0:Proveedor"] = "Dropbox",
            ["Dev3:VinculosIdentidad:0:IdentificadorExterno"] = externalA,
            ["Dev3:VinculosIdentidad:0:UsuarioId"] = userA.ToString(),
            ["Dev3:VinculosIdentidad:1:Proveedor"] = "Microsoft",
            ["Dev3:VinculosIdentidad:1:IdentificadorExterno"] = externalB,
            ["Dev3:VinculosIdentidad:1:UsuarioId"] = userB.ToString()
        });
        builder.WebHost.UseUrls("https://127.0.0.1:0");
        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
        builder.Services.AddAuthentication().AddCookie(Dev3Authentication.LocalScheme, options =>
        {
            options.Cookie.Name = "Dev3.Test.Cookie";
            options.Events.OnRedirectToLogin = context =>
            {
                context.Response.StatusCode = 401;
                return Task.CompletedTask;
            };
        });
        builder.Services.AddDev3Authentication(builder.Configuration);
        builder.Services.AddAuthorization();
        builder.Services.AddOptions<VinculosIdentidadDev3Options>()
            .Bind(builder.Configuration.GetSection("Dev3"))
            .Validate(options => options.EsValida()).ValidateOnStart();
        builder.Services.AddScoped<AccesoDev3Filter>();
        builder.Services.AddDbContext<FinanzasDbContext>(options => options.UseNpgsql(args[0]));
        builder.Services.AddControllersWithViews().AddApplicationPart(typeof(PresupuestosMensualesController).Assembly);
        builder.Services.AddOpenApi();

        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapOpenApi();
        app.MapScalarApiReference();

        await using (var scope = app.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FinanzasDbContext>();
            // Solo fixture desechable: DDL del modelo existente. No es una migración de la API.
            await db.Database.ExecuteSqlRawAsync(db.Database.GenerateCreateScript());
            db.Usuarios.AddRange(
                new Usuario { Id = userA, Nombre = "Fixture A", Email = "fixture-a@example.invalid" },
                new Usuario { Id = userB, Nombre = "Fixture B", Email = "fixture-b@example.invalid" });
            db.Cuentas.AddRange(
                new Cuenta { Id = accountA, UsuarioId = userA, Nombre = "A", Tipo = "banco", SaldoInicial = 100m },
                new Cuenta { Id = accountB, UsuarioId = userB, Nombre = "B", Tipo = "banco", SaldoInicial = 200m });
            db.Categorias.AddRange(
                new Categoria { Id = categoryA, UsuarioId = userA, Nombre = "Gasto A", Tipo = "gasto" },
                new Categoria { Id = categoryB, UsuarioId = userB, Nombre = "Gasto B", Tipo = "gasto" },
                new Categoria { Id = incomeA, UsuarioId = userA, Nombre = "Ingreso A", Tipo = "ingreso" },
                new Categoria { Id = inactiveA, UsuarioId = userA, Nombre = "Inactiva A", Tipo = "gasto", Activa = false });
            await db.SaveChangesAsync();
        }

        await app.StartAsync();
        try
        {
            var url = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
            HttpClient Client(string? provider = null, string? external = null)
            {
                var handler = new HttpClientHandler
                {
                    AllowAutoRedirect = false,
                    // Exclusivamente el host local desechable; no altera TLS de OAuth.
                    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                var client = new HttpClient(handler) { BaseAddress = new Uri(url) };
                if (provider is not null)
                {
                    var claims = new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, external!),
                        new Claim(Dev3Authentication.ProviderClaim, provider),
                        new Claim(ClaimTypes.Name, "Identidad simulada de integración")
                    };
                    var ticket = new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims,
                        Dev3Authentication.LocalScheme)), new AuthenticationProperties(), Dev3Authentication.LocalScheme);
                    var options = app.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                        .Get(Dev3Authentication.LocalScheme);
                    client.DefaultRequestHeaders.Add("Cookie", options.Cookie.Name + "=" + options.TicketDataFormat.Protect(ticket));
                }
                return client;
            }

            async Task Csrf(HttpClient client)
            {
                var page = await client.GetStringAsync("/auth/dev3");
                var match = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
                Check(match.Success, "La página debe emitir antiforgery.");
                client.DefaultRequestHeaders.Remove("RequestVerificationToken");
                client.DefaultRequestHeaders.Add("RequestVerificationToken", WebUtility.HtmlDecode(match.Groups[1].Value));
            }

            async Task<JsonElement> Json(HttpResponseMessage response) =>
                JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();

            var routes = new[] { "presupuestos-mensuales", "metas-ahorro", "movimientos-recurrentes", "recordatorios-pago" };
            using var anonymous = Client();
            foreach (var route in routes)
            {
                Check((int)(await anonymous.GetAsync("/api/" + route)).StatusCode == 401, "GET lista sin sesión debe ser 401.");
                Check((int)(await anonymous.GetAsync("/api/" + route + "/" + Guid.NewGuid())).StatusCode == 401, "GET ID sin sesión debe ser 401.");
                Check((int)(await anonymous.PostAsJsonAsync("/api/" + route, new { })).StatusCode == 401, "POST sin sesión debe ser 401.");
            }
            using var unlinked = Client("Dropbox", "dropbox|no-vinculado");
            Check((int)(await unlinked.GetAsync("/api/metas-ahorro")).StatusCode == 403, "Una sesión no vinculada no debe acceder.");
            using var first = Client("Dropbox", externalA);
            using var second = Client("Microsoft", externalB);
            await Csrf(first);
            await Csrf(second);
            using var withoutCsrf = Client("Dropbox", externalA);
            Check((int)(await withoutCsrf.PostAsJsonAsync("/api/metas-ahorro", new { nombre = "Sin CSRF", montoObjetivo = 10 })).StatusCode == 400,
                "POST con cookie pero sin CSRF debe rechazarse.");

            var ids = new Dictionary<string, Guid[]>();
            for (var index = 0; index < 2; index++)
            {
                var client = index == 0 ? first : second;
                var user = index == 0 ? userA : userB;
                var account = index == 0 ? accountA : accountB;
                var category = index == 0 ? categoryA : categoryB;
                var own = new List<Guid>();
                object[] payloads =
                {
                    new { categoriaId = category, año = 2026, mes = 11, montoLimite = 50m },
                    new { cuentaVinculadaId = account, nombre = "Meta fixture", montoObjetivo = 80m },
                    new { cuentaId = account, categoriaId = category, tipo = "gasto", montoEstimado = 5m,
                        concepto = "Plantilla fixture", frecuencia = "mensual", diaEjecucion = 20, proximaFecha = "2026-12-20", activo = false },
                    new { titulo = "Recordatorio fixture", fechaVencimiento = "2026-12-20" }
                };
                for (var i = 0; i < routes.Length; i++)
                {
                    var response = await client.PostAsJsonAsync("/api/" + routes[i], payloads[i]);
                    Check((int)response.StatusCode == 201,
                        "POST " + routes[i] + " esperaba 201, obtuvo " + (int)response.StatusCode + ": " + await response.Content.ReadAsStringAsync());
                    var body = await Json(response);
                    Check(body.GetProperty("usuarioId").GetGuid() == user, "UsuarioId debe proceder del servidor.");
                    var id = body.GetProperty("id").GetGuid();
                    own.Add(id);
                    Check(response.Headers.Location is not null, "201 debe incluir Location.");
                    Check((await client.GetAsync(response.Headers.Location)).StatusCode == HttpStatusCode.OK, "GET Location debe devolver 200.");
                    var list = await Json(await client.GetAsync("/api/" + routes[i]));
                    Check(list.GetArrayLength() == 1 && list[0].GetProperty("usuarioId").GetGuid() == user, "Lista debe contener solo registros propios.");
                    Check((int)(await client.GetAsync("/api/" + routes[i] + "/" + Guid.NewGuid())).StatusCode == 404, "ID inexistente debe ser 404.");
                }
                ids[index.ToString()] = own.ToArray();
            }
            for (var i = 0; i < routes.Length; i++)
            {
                Check((int)(await first.GetAsync("/api/" + routes[i] + "/" + ids["1"][i])).StatusCode == 404, "Datos ajenos deben ser 404.");
                Check((int)(await second.GetAsync("/api/" + routes[i] + "/" + ids["0"][i])).StatusCode == 404, "Aislamiento debe ser bidireccional.");
                var list = await Json(await first.GetAsync("/api/" + routes[i]));
                Check(list.GetArrayLength() == 1, "La lista no debe filtrar datos del segundo usuario.");
            }

            async Task Reject(string route, object payload, int expected = 400) => Check(
                (int)(await first.PostAsJsonAsync("/api/" + route, payload)).StatusCode == expected, "Validación falló en " + route);
            await Reject(routes[0], new { categoriaId = categoryA, año = 2026, mes = 11, montoLimite = 0 }, 409);
            await Reject(routes[0], new { categoriaId = categoryB, año = 2026, mes = 12, montoLimite = 1 });
            await Reject(routes[0], new { categoriaId = incomeA, año = 2026, mes = 12, montoLimite = 1 });
            await Reject(routes[0], new { categoriaId = inactiveA, año = 2026, mes = 12, montoLimite = 1 });
            await Reject(routes[0], new { categoriaId = categoryA, año = 0, mes = 12, montoLimite = 1 });
            await Reject(routes[0], new { categoriaId = categoryA, año = 2026, mes = 13, montoLimite = 1 });
            await Reject(routes[0], new { categoriaId = categoryA, año = 2026, mes = 12, montoLimite = -1 });
            await Reject(routes[1], new { nombre = "No", montoObjetivo = 10, cuentaVinculadaId = accountB });
            await Reject(routes[1], new { nombre = " ", montoObjetivo = 10 });
            await Reject(routes[1], new { nombre = "No", montoObjetivo = 0 });
            await Reject(routes[1], new { nombre = "No", montoObjetivo = 1.001m });
            await Reject(routes[1], new { nombre = "No", montoObjetivo = 1000000000000m });
            await Reject(routes[1], new { nombre = "No", montoObjetivo = 10, montoAhorrado = -1 });
            await Reject(routes[1], new { nombre = "No", montoObjetivo = 10, usuarioId = userB });
            await Reject(routes[2], new { cuentaId = accountB, categoriaId = categoryA, tipo = "gasto", montoEstimado = 1,
                concepto = "No", frecuencia = "mensual", diaEjecucion = 2, proximaFecha = "2026-12-02" });
            await Reject(routes[2], new { cuentaId = accountA, categoriaId = incomeA, tipo = "gasto", montoEstimado = 1,
                concepto = "No", frecuencia = "mensual", diaEjecucion = 2, proximaFecha = "2026-12-02" });
            await Reject(routes[2], new { cuentaId = accountA, categoriaId = categoryA, tipo = "gasto", montoEstimado = 1,
                concepto = "No", frecuencia = "semanal", diaEjecucion = 8, proximaFecha = "2026-12-02" });
            await Reject(routes[3], new { titulo = "No", fechaVencimiento = "2026-12-20", movimientoRecurrenteId = ids["1"][2] });
            await Reject(routes[3], new { titulo = "No", fechaVencimiento = "0001-01-01" });
            await Reject(routes[3], new { titulo = "No", fechaVencimiento = "2026-12-20", montoEstimado = -1 });
            await Reject(routes[3], new { titulo = "No" });

            await using (var scope = app.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<FinanzasDbContext>();
                Check(await db.PresupuestosMensuales.AsNoTracking().CountAsync() == 2, "Persistencia real de presupuestos.");
                Check(await db.MetasAhorro.AsNoTracking().CountAsync() == 2, "Persistencia real de metas.");
                Check(await db.MovimientosRecurrentes.AsNoTracking().CountAsync() == 2, "Persistencia real de recurrencias.");
                Check(await db.RecordatoriosPago.AsNoTracking().CountAsync() == 2, "Persistencia real de recordatorios.");
                Check(!await db.MovimientosRecurrentes.AnyAsync(x => x.Activo), "Activo=false debe persistir, no sustituirse por el default.");
                Check(await db.Movimientos.CountAsync() == 0, "No crear movimientos reales.");
                Check(await db.Cuentas.Where(x => x.Id == accountA).Select(x => x.SaldoInicial).SingleAsync() == 100m, "No cambiar saldos.");
                var local = await db.Usuarios.SingleAsync(x => x.Id == userA);
                local.Activo = false;
                await db.SaveChangesAsync();
            }
            Check((int)(await first.GetAsync("/api/metas-ahorro")).StatusCode == 403, "Usuario desactivado no puede acceder con cookie antigua.");

            var document = await Json(await anonymous.GetAsync("/openapi/v1.json"));
            foreach (var route in routes)
            {
                var paths = document.GetProperty("paths");
                Check(paths.GetProperty("/api/" + route).TryGetProperty("get", out _), "OpenAPI debe mostrar GET lista.");
                Check(paths.GetProperty("/api/" + route).TryGetProperty("post", out _), "OpenAPI debe mostrar POST.");
                Check(paths.GetProperty("/api/" + route + "/{id}").TryGetProperty("get", out _), "OpenAPI debe mostrar GET ID.");
            }
            Check((await anonymous.GetAsync("/scalar/v1")).IsSuccessStatusCode, "Scalar debe conservarse.");
            Console.WriteLine($"OK: {assertions} comprobaciones con PostgreSQL real aislado y cookies de prueba para Dropbox/Microsoft.");
            Console.WriteLine("NO se probó un login interactivo real con los proveedores. NO se modificó la base de desarrollo.");
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
