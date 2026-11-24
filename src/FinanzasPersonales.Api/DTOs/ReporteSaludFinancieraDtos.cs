using System.Text.Json.Serialization;

namespace FinanzasPersonales.Api.DTOs;

public sealed record ReporteSaludFinancieraDto(
    Guid UsuarioId,
    DateTimeOffset FechaGeneracion,
    DiagnosticoFinancieroDto Diagnostico,
    ResumenMetasDto MetasAhorro,
    ResumenPresupuestosDto Presupuestos,
    ResumenRecurrenciasDto CompromisosRecurrentes,
    ResumenRecordatoriosDto RecordatoriosPago
);

public sealed record DiagnosticoFinancieroDto(
    string NivelSalud, // "Excelente", "Estable", "Atención requerida", "Alerta"
    string ColorHex,
    string Mensaje,
    decimal RatioAhorroVsGastosRecurrentes
);

public sealed record ResumenMetasDto(
    decimal TotalMontoObjetivo,
    decimal TotalMontoAhorrado,
    decimal PorcentajeAvanceGlobal,
    int TotalMetas,
    int MetasActivas,
    int MetasCompletadas,
    List<ItemMetaReporteDto> Detalle
);

public sealed record ItemMetaReporteDto(
    Guid Id,
    string Nombre,
    decimal MontoObjetivo,
    decimal MontoAhorrado,
    decimal PorcentajeAvance,
    DateOnly? FechaLimite,
    string Estado
);

public sealed record ResumenPresupuestosDto(
    decimal TotalPresupuestadoMesActual,
    int CantidadPresupuestos,
    short AñoActual,
    short MesActual,
    List<ItemPresupuestoReporteDto> Detalle
);

public sealed record ItemPresupuestoReporteDto(
    Guid Id,
    Guid CategoriaId,
    short Año,
    short Mes,
    decimal MontoLimite
);

public sealed record ResumenRecurrenciasDto(
    decimal TotalGastosMensualesEstimados,
    decimal TotalIngresosMensualesEstimados,
    decimal FlujoNetoMensualEstimado,
    int TotalRecurrenciasActivas,
    List<ItemRecurrenciaReporteDto> Detalle
);

public sealed record ItemRecurrenciaReporteDto(
    Guid Id,
    string Concepto,
    string Tipo,
    decimal MontoEstimado,
    string Frecuencia,
    DateOnly ProximaFecha,
    bool Activo
);

public sealed record ResumenRecordatoriosDto(
    decimal TotalMontoPendiente,
    decimal TotalMontoPagado,
    int TotalRecordatorios,
    int RecordatoriosVencidos,
    int RecordatoriosPendientes,
    int RecordatoriosPagados,
    List<ItemRecordatorioReporteDto> Detalle
);

public sealed record ItemRecordatorioReporteDto(
    Guid Id,
    string Titulo,
    decimal? MontoEstimado,
    DateOnly FechaVencimiento,
    bool Pagado,
    bool Vencido,
    DateOnly? FechaPago,
    string? Notas
);
