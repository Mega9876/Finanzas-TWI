using FinanzasPersonales.Api.Data;
using FinanzasPersonales.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanzasPersonales.Api;

public class ReportesService
{
    private readonly FinanzasDbContext _db;

    public ReportesService(FinanzasDbContext db)
    {
        _db = db;
    }

    public async Task<ReporteCuentasFlujoDto?> ObtenerCuentasYFlujoPorEmailAsync(
        string email,
        string? nombre = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email))
            return null;

        var emailNormalizado = email.Trim().ToLowerInvariant();

        var usuario = await _db.Usuarios
            .FirstOrDefaultAsync(u => u.Email.ToLower() == emailNormalizado, ct);

        if (usuario is null)
        {
            var nombreResuelto = !string.IsNullOrWhiteSpace(nombre)
                ? nombre.Trim()
                : emailNormalizado.Split('@')[0];

            usuario = new Usuario
            {
                Id = Guid.NewGuid(),
                Nombre = nombreResuelto,
                Email = emailNormalizado,
                PasswordHash = string.Empty,
                FechaCreacion = DateTimeOffset.UtcNow,
                Activo = true
            };

            _db.Usuarios.Add(usuario);
            await _db.SaveChangesAsync(ct);
        }
        else if (!usuario.Activo)
        {
            return null;
        }

        return await ObtenerCuentasYFlujoAsync(usuario.Id, ct);
    }

    public async Task<ReporteCuentasFlujoDto?> ObtenerCuentasYFlujoAsync(
        Guid usuarioId,
        CancellationToken ct = default)
    {
        var usuario = await _db.Usuarios
            .AsNoTracking()
            .Where(u => u.Id == usuarioId && u.Activo)
            .Select(u => new
            {
                u.Id,
                u.Nombre,
                u.Email
            })
            .FirstOrDefaultAsync(ct);

        if (usuario is null)
            return null;

        var cuentasDatos = await _db.Cuentas
            .AsNoTracking()
            .Where(c => c.UsuarioId == usuarioId)
            .OrderByDescending(c => c.Activa)
            .ThenBy(c => c.Nombre)
            .Select(c => new
            {
                c.Id,
                c.Nombre,
                c.Tipo,
                c.Moneda,
                c.Activa,
                c.SaldoInicial,
                Ingresos = _db.Movimientos
                    .Where(m => m.UsuarioId == usuarioId && m.CuentaId == c.Id && m.Tipo == "ingreso")
                    .Sum(m => (decimal?)m.Monto) ?? 0m,
                Gastos = _db.Movimientos
                    .Where(m => m.UsuarioId == usuarioId && m.CuentaId == c.Id && m.Tipo == "gasto")
                    .Sum(m => (decimal?)m.Monto) ?? 0m,
                CantidadMovimientos = _db.Movimientos
                    .Count(m => m.UsuarioId == usuarioId && m.CuentaId == c.Id)
            })
            .ToListAsync(ct);

        var cuentasDto = new List<CuentaReporteDto>(cuentasDatos.Count);

        foreach (var c in cuentasDatos)
        {
            var saldoActual = c.SaldoInicial + c.Ingresos - c.Gastos;

            cuentasDto.Add(new CuentaReporteDto
            {
                CuentaId = c.Id,
                Nombre = c.Nombre,
                Tipo = c.Tipo,
                Moneda = c.Moneda,
                Activa = c.Activa,
                SaldoInicial = c.SaldoInicial,
                TotalIngresos = c.Ingresos,
                TotalGastos = c.Gastos,
                SaldoActual = saldoActual,
                CantidadMovimientos = c.CantidadMovimientos
            });
        }

        var totalesPorMoneda = cuentasDto
            .GroupBy(x => x.Moneda)
            .Select(g =>
            {
                var saldoInicialTotal = g.Sum(x => x.SaldoInicial);
                var ingresosTotal = g.Sum(x => x.TotalIngresos);
                var gastosTotal = g.Sum(x => x.TotalGastos);
                var flujoNeto = ingresosTotal - gastosTotal;
                var saldoActualTotal = g.Sum(x => x.SaldoActual);

                return new ResumenMonedaDto
                {
                    Moneda = g.Key,
                    SaldoInicialTotal = saldoInicialTotal,
                    TotalIngresos = ingresosTotal,
                    TotalGastos = gastosTotal,
                    FlujoNeto = flujoNeto,
                    SaldoActualTotal = saldoActualTotal
                };
            })
            .ToList();

        return new ReporteCuentasFlujoDto
        {
            UsuarioId = usuario.Id,
            UsuarioNombre = usuario.Nombre,
            UsuarioEmail = usuario.Email,
            TotalesPorMoneda = totalesPorMoneda,
            Cuentas = cuentasDto
        };
    }
}