using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Consultas de solo lectura: estado de cuenta (RF034) y oferta académica (RF035)
[ApiController]
[Route("api/consultas")]
public class ConsultasController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public ConsultasController(ApplicationDbContext db) => _db = db;

    // =====================================================================
    //  ESTADO DE CUENTA DEL ESTUDIANTE (RF034)
    // =====================================================================

    // GET api/consultas/estudiantes?texto=jos
    // Busca estudiantes por nombre, cédula o número de carné (habilitados o no)
    // GET api/consultas/estudiantes?texto=jos
    // Busca estudiantes por nombre, cédula o número de carné (habilitados o no).
    // Si no se envía texto, devuelve a TODOS los estudiantes.
    [HttpGet("estudiantes")]
    public async Task<IActionResult> BuscarEstudiantes([FromQuery] string? texto)
    {
        var s = texto?.Trim();
        var q = _db.Estudiantes.AsNoTracking().AsQueryable();

        // Solo se filtra si hay texto; sin texto se listan todos
        if (!string.IsNullOrEmpty(s))
        {
            q = q.Where(e =>
                EF.Functions.Collate(e.IdPersonaNavigation.NombreCompleto, "Latin1_General_CI_AI").Contains(s) ||
                (e.NumeroCarne != null && e.NumeroCarne.Contains(s)) ||
                (e.IdPersonaNavigation.Cedula != null && e.IdPersonaNavigation.Cedula.Contains(s)));
        }

        var consulta = q
            .OrderBy(e => e.IdPersonaNavigation.NombreCompleto)
            .Select(e => new
            {
                e.IdEstudiante,
                Nombre = e.IdPersonaNavigation.NombreCompleto,
                e.NumeroCarne,
                Cedula = e.IdPersonaNavigation.Cedula,
                Habilitado = e.Estado
            });

        // Con texto se limita a 50 coincidencias; el listado completo no lleva límite
        var resultados = string.IsNullOrEmpty(s)
            ? await consulta.ToListAsync()
            : await consulta.Take(50).ToListAsync();

        return Ok(new
        {
            resultados,
            mensaje = resultados.Count == 0
                ? (string.IsNullOrEmpty(s) ? "Todavía no hay estudiantes registrados." : "No se encontraron resultados.")
                : null
        });
    }

    // GET api/consultas/estado-cuenta/{idEstudiante}
    // GET api/consultas/estado-cuenta/{idEstudiante}?idPeriodo=1   (filtra por período)
    [HttpGet("estado-cuenta/{idEstudiante:int}")]
    public async Task<IActionResult> EstadoCuenta(int idEstudiante, [FromQuery] int? idPeriodo)
    {
        var estudiante = await _db.Estudiantes.AsNoTracking()
       .Where(e => e.IdEstudiante == idEstudiante)
       .Select(e => new
       {
           e.IdEstudiante,
           Nombre = e.IdPersonaNavigation.NombreCompleto,
           e.NumeroCarne,
           Habilitado = e.Estado
       })
       .FirstOrDefaultAsync()
       ?? throw new NegocioException("El estudiante no existe.");

        // Matrículas del estudiante (todas, o solo las del período pedido)
        var q = _db.Matriculas.AsNoTracking().Where(m => m.IdEstudiante == idEstudiante);
        if (idPeriodo is not null) q = q.Where(m => m.IdPeriodo == idPeriodo);

        var matriculas = await q
            .OrderBy(m => m.IdPeriodoNavigation.FechaInicioCurso).ThenBy(m => m.IdMatricula)
            .Select(m => new
            {
                m.IdMatricula,
                m.IdPeriodo,
                Curso = m.IdPeriodoNavigation.IdCursoNavigation.Nombre,
                Modalidad = m.IdPeriodoNavigation.IdModalidadNavigation.Nombre,
                FechaInicioCurso = m.IdPeriodoNavigation.FechaInicioCurso,
                FechaFinCurso = m.IdPeriodoNavigation.FechaFinCurso,
                Grupo = m.Grupo.Identificacion,
                m.Estado
            })
            .ToListAsync();

        if (matriculas.Count == 0)
            return Ok(new
            {
                estudiante,
                periodos = Array.Empty<object>(),
                mensaje = idPeriodo is null
                    ? "El estudiante no posee matrículas registradas."
                    : "No se encontraron registros para el período seleccionado."
            });

        var ids = matriculas.Select(m => m.IdMatricula).ToList();

        // Conceptos de cada matrícula (la vista ya excluye los anulados)
        var obligaciones = await _db.VwEstadoCuenta.AsNoTracking()
            .Where(v => ids.Contains(v.IdMatricula))
            .OrderBy(v => v.IdObligacion)
            .ToListAsync();

        // Totales financieros por matrícula, en dólares
        var totales = await _db.VwTotalesFinancieros.AsNoTracking()
            .Where(v => ids.Contains(v.IdMatricula))
            .ToDictionaryAsync(v => v.IdMatricula);

        // Historial de pagos, con el comprobante y los conceptos abonados
        var pagos = await _db.Pagos.AsNoTracking()
            .Where(p => ids.Contains(p.IdMatricula))
            .OrderBy(p => p.FechaPago).ThenBy(p => p.IdPago)
            .Select(p => new
            {
                p.IdMatricula,
                p.IdPago,
                Comprobante = p.Comprobante != null ? p.Comprobante.Numero : null,
                p.FechaPago,
                p.MonedaRecibida,
                p.TipoCambio,
                p.TotalRecibido,
                p.TotalEquivalenteUsd,
                p.Estado,
                Conceptos = p.DetallePagos.Select(d => new
                {
                    Concepto = d.ObligacionPago.Descripcion,
                    d.MontoAbonado,
                    d.MontoEquivalenteUsd
                }).ToList()
            })
            .ToListAsync();

        // Armamos un bloque por matrícula, sin mezclar períodos
        var periodos = matriculas.Select(m =>
        {
            var pagosMatricula = pagos.Where(p => p.IdMatricula == m.IdMatricula).ToList();
            totales.TryGetValue(m.IdMatricula, out var t);

            return new
            {
                m.IdMatricula,
                m.IdPeriodo,
                m.Curso,
                m.Modalidad,
                m.FechaInicioCurso,
                m.FechaFinCurso,
                m.Grupo,
                EstadoMatricula = m.Estado ? "Habilitada" : "Inhabilitada",
                conceptos = obligaciones
                    .Where(o => o.IdMatricula == m.IdMatricula)
                    .Select(o => new
                    {
                        o.IdObligacion,
                        o.Descripcion,
                        Mes = o.MesCorrespondiente,
                        o.PrecioOriginalUsd,
                        o.DescuentoUsd,
                        o.PrecioFinalUsd,
                        o.TotalAbonadoUsd,
                        o.SaldoPendienteUsd,
                        Estado = o.EstadoMostrar        // Cancelado, Pendiente o Parcialmente pagado
                    }).ToList(),
                totales = t is null ? null : new
                {
                    t.TotalOriginalUsd,
                    t.TotalDescuentosUsd,
                    t.TotalAPagarUsd,
                    t.TotalRecibidoUsd,
                    t.SaldoPendienteUsd
                },
                pagos = pagosMatricula,
                mensajePagos = pagosMatricula.Count == 0
                    ? "No existen pagos registrados para esta matrícula."
                    : null
            };
        }).ToList();

        return Ok(new { estudiante, periodos, mensaje = (string?)null });
    }

    // =====================================================================
    //  OFERTA ACADÉMICA (RF035)
    // =====================================================================

    // GET api/consultas/oferta/cursos
    // Cursos con al menos un grupo habilitado en un período habilitado y vigente
    [HttpGet("oferta/cursos")]
    public async Task<IActionResult> OfertaCursos()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // La vista ya trae solo grupos habilitados de períodos habilitados;
        // agregamos que el período no haya terminado
        var filas = await _db.VwOfertaAcademicas.AsNoTracking()
            .Where(v => v.FechaFinCurso >= hoy)
            .ToListAsync();

        var cursos = filas
            .GroupBy(v => new { v.IdCurso, v.Curso })
            .OrderBy(g => g.Key.Curso)
            .Select(g => new { g.Key.IdCurso, g.Key.Curso, Grupos = g.Count() })
            .ToList();

        return Ok(new
        {
            cursos,
            mensaje = cursos.Count == 0 ? "No hay oferta académica vigente disponible." : null
        });
    }

    // GET api/consultas/oferta/cursos/{idCurso}/grupos
    // Grupos vigentes de un curso
    [HttpGet("oferta/cursos/{idCurso:int}/grupos")]
    public async Task<IActionResult> OfertaGrupos(int idCurso)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var grupos = await _db.VwOfertaAcademicas.AsNoTracking()
            .Where(v => v.IdCurso == idCurso && v.FechaFinCurso >= hoy)
            .OrderBy(v => v.Identificacion)
            .Select(v => new
            {
                v.IdGrupo,
                v.Identificacion,
                v.Nivel,
                v.Modalidad,
                v.Horario,
                v.Docente,
                v.Inscritos,
                v.CuposDisponibles
            })
            .ToListAsync();

        return Ok(new
        {
            grupos,
            mensaje = grupos.Count == 0 ? "No hay grupos disponibles para ese curso." : null
        });
    }

    // GET api/consultas/oferta/grupos/{idGrupo}
    // Detalle de un grupo vigente, con su lista de estudiantes inscritos
    [HttpGet("oferta/grupos/{idGrupo:int}")]
    public async Task<IActionResult> OfertaGrupo(int idGrupo)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var grupo = await _db.VwOfertaAcademicas.AsNoTracking()
            .FirstOrDefaultAsync(v => v.IdGrupo == idGrupo && v.FechaFinCurso >= hoy)
            ?? throw new NegocioException("El grupo no forma parte de la oferta académica vigente.");

        // Solo nombre completo y carné de los estudiantes con matrícula habilitada
        var estudiantes = await _db.Matriculas.AsNoTracking()
            .Where(m => m.IdGrupo == idGrupo && m.Estado)
            .OrderBy(m => m.IdEstudianteNavigation.IdPersonaNavigation.NombreCompleto)
            .Select(m => new
            {
                Nombre = m.IdEstudianteNavigation.IdPersonaNavigation.NombreCompleto,
                m.IdEstudianteNavigation.NumeroCarne
            })
            .ToListAsync();

        return Ok(new
        {
            grupo = new
            {
                grupo.IdGrupo,
                grupo.Curso,
                grupo.Nivel,
                grupo.Identificacion,
                grupo.Modalidad,
                grupo.Horario,
                grupo.Aula,
                grupo.Docente,
                grupo.FechaInicioCurso,
                grupo.FechaFinCurso,
                EstadoGrupo = "Habilitado",       // la vista solo incluye grupos y períodos habilitados
                EstadoPeriodo = "Habilitado",
                grupo.Inscritos,
                grupo.Capacidad,
                grupo.CuposDisponibles
            },
            estudiantes,
            mensaje = estudiantes.Count == 0 ? "No existen estudiantes inscritos en este grupo." : null
        });
    }
}