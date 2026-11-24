namespace FinanzasPersonales.Api;

public class CuentaReporteDto
{
    public Guid CuentaId { get; set; }
    public string Nombre { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Moneda { get; set; } = string.Empty;
    public bool Activa { get; set; }
    public decimal SaldoInicial { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalGastos { get; set; }
    public decimal SaldoActual { get; set; }
    public int CantidadMovimientos { get; set; }
}

public class ResumenMonedaDto
{
    public string Moneda { get; set; } = string.Empty;
    public decimal SaldoInicialTotal { get; set; }
    public decimal TotalIngresos { get; set; }
    public decimal TotalGastos { get; set; }
    public decimal FlujoNeto { get; set; }
    public decimal SaldoActualTotal { get; set; }
}

public class ReporteCuentasFlujoDto
{
    public Guid UsuarioId { get; set; }
    public string UsuarioNombre { get; set; } = string.Empty;
    public string UsuarioEmail { get; set; } = string.Empty;
    public List<ResumenMonedaDto> TotalesPorMoneda { get; set; } = new();
    public List<CuentaReporteDto> Cuentas { get; set; } = new();
}
 
