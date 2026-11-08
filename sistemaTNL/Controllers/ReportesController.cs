using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Datos básicos del período que encabezan los reportes
public record PeriodoInfo(int IdPeriodo, string Curso, string Modalidad, DateOnly FechaFinInscripcion, string Estado);

// Reportes gerenciales: solo consultan, no modifican nada
[ApiController]
[Route("api/reportes")]
public class ReportesController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ReportesController(ApplicationDbContext db) => _db = db;

    // GET api/reportes/estudiantes-activos?idPeriodo=1  (RF031)
    // Total de estudiantes activos del período, desglosado por nivel
    [HttpGet("estudiantes-activos")]
    public async Task<IActionResult> EstudiantesActivos([FromQuery] int idPeriodo)
    {
        var periodo = await InfoPeriodoAsync(idPeriodo);

        // ¿Hay matrículas habilitadas en el período?
        var hayMatriculas = await _db.Matriculas.AnyAsync(m => m.IdPeriodo == idPeriodo && m.Estado);
        if (!hayMatriculas)
            return Ok(new
            {
                periodo,
                total = 0,
                porNivel = Array.Empty<object>(),
                mensaje = "No hay estudiantes matriculados para el período consultado."
            });

        // La vista ya aplica la regla: matrícula habilitada + concepto Matrícula pagado
        var filas = await _db.VwEstudianteActivos.AsNoTracking()
            .Where(v => v.IdPeriodo == idPeriodo)
            .ToListAsync();

        var niveles = await _db.Nivels.AsNoTracking().ToDictionaryAsync(n => n.IdNivel, n => n.Nombre);

        // Contamos a cada estudiante una sola vez
        var porNivel = filas
            .GroupBy(v => v.IdNivel)
            .Select(g => new
            {
                IdNivel = g.Key,
                Nivel = niveles.GetValueOrDefault(g.Key) ?? "",
                Cantidad = g.Select(x => x.IdEstudiante).Distinct().Count()
            })
            .OrderBy(x => x.IdNivel)
            .ToList();

        var total = filas.Select(v => v.IdEstudiante).Distinct().Count();

        return Ok(new
        {
            periodo,
            total,
            porNivel,
            mensaje = total == 0
                ? "Hay matrículas en el período, pero ningún estudiante ha pagado la matrícula todavía."
                : null
        });
    }

    // GET api/reportes/ingresos-mensuales?anio=2026&mes=11  (RF032)
    // Total recibido en el mes, en córdobas, con el tipo de cambio de cada transacción
    [HttpGet("ingresos-mensuales")]
    public async Task<IActionResult> IngresosMensuales([FromQuery] int anio, [FromQuery] int mes)
    {
        if (mes < 1 || mes > 12)
            throw new NegocioException("El mes debe estar entre 1 y 12.");
        if (anio < 2000 || anio > 2100)
            throw new NegocioException("El año no es válido.");

        // La vista suma solo pagos Registrados (los anulados no cuentan)
        var fila = await _db.VwIngresosMensuales.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Anio == anio && v.Mes == mes);

        if (fila is null)
            return Ok(new
            {
                anio,
                mes,
                totalIngresosNio = 0.00m,
                totalIngresosUsd = 0.00m,
                cantidadPagos = 0,
                mensaje = "No se encontraron ingresos para ese período."
            });

        return Ok(new
        {
            anio,
            mes,
            totalIngresosNio = fila.TotalIngresosNio,
            totalIngresosUsd = fila.TotalIngresosUsd,
            cantidadPagos = fila.CantidadPagos,
            mensaje = (string?)null
        });
    }

    // GET api/reportes/desercion?idPeriodo=1  (RF033)
    // Tasa de deserción por nivel. Solo se puede calcular cuando terminó la inscripción.
    [HttpGet("desercion")]
    public async Task<IActionResult> Desercion([FromQuery] int idPeriodo)
    {
        var periodo = await InfoPeriodoAsync(idPeriodo);

        // Mientras la inscripción siga abierta, todavía no se sabe quién desertó
        if (DateOnly.FromDateTime(DateTime.Today) <= periodo.FechaFinInscripcion)
            return Ok(new
            {
                periodo,
                niveles = Array.Empty<object>(),
                mensaje = "La inscripción del período aún no ha finalizado; todavía no es posible determinar la deserción."
            });

        var filas = await _db.VwDesercions.AsNoTracking()
            .Where(v => v.IdPeriodo == idPeriodo)
            .ToListAsync();

        if (filas.Count == 0)
            return Ok(new
            {
                periodo,
                niveles = Array.Empty<object>(),
                mensaje = "No hay datos suficientes para calcular la tasa de deserción de este período."
            });

        var nombres = await _db.Nivels.AsNoTracking().ToDictionaryAsync(n => n.IdNivel, n => n.Nombre);

        var niveles = filas
            .OrderBy(f => f.IdNivel)
            .Select(f => new
            {
                f.IdNivel,
                Nivel = nombres.GetValueOrDefault(f.IdNivel) ?? "",
                EstudiantesDebianContinuar = f.EstudiantesDebianContinuar,
                Desertores = f.EstudiantesSinMatricula,
                TasaDesercionPct = f.TasaDesercionPct
            })
            .ToList();

        return Ok(new { periodo, niveles, mensaje = (string?)null });
    }

    // ---------- Apoyo ----------

    // Datos del período; si no existe, mensaje claro
    private async Task<PeriodoInfo> InfoPeriodoAsync(int idPeriodo) =>
        await _db.PeriodoInscripcions.AsNoTracking()
            .Where(p => p.IdPeriodo == idPeriodo)
            .Select(p => new PeriodoInfo(
                p.IdPeriodo,
                p.IdCursoNavigation.Nombre,
                p.IdModalidadNavigation.Nombre,
                p.FechaFinInscripcion,
                p.Estado))
            .FirstOrDefaultAsync()
        ?? throw new NegocioException("El período no existe.");
}