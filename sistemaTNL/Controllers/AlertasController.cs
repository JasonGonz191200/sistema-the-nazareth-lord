using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;

namespace sistemaTNL.Controllers;

// Alertas de grupos y períodos (solo lectura: no modifica nada)
[ApiController]
[Route("api/alertas")]
public class AlertasController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AlertasController(ApplicationDbContext db) => _db = db;

    // Dias que faltan para el fin de inscripción en los que la alerta pasa a "Urgente"
    private const int DiasUrgente = 7;

    // GET api/alertas
    [HttpGet]
    public async Task<IActionResult> Obtener()
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        // Mínimo y máximo de estudiantes por grupo, leídos de la tabla parametro
        var min = await LeerParametroAsync("MIN_ESTUDIANTES_GRUPO", 8);
        var max = await LeerParametroAsync("MAX_ESTUDIANTES_GRUPO", 25);

        // Solo períodos habilitados (con el nombre del curso y la modalidad)
        var periodos = await _db.PeriodoInscripcions.AsNoTracking()
            .Where(p => p.Estado == "Habilitado")
            .Select(p => new
            {
                p.IdPeriodo,
                Curso = p.IdCursoNavigation.Nombre,
                Modalidad = p.IdModalidadNavigation.Nombre,
                p.FechaFinInscripcion
            })
            .ToListAsync();

        var idsPeriodos = periodos.Select(p => p.IdPeriodo).ToList();

        // Grupos habilitados de esos períodos
        var grupos = await _db.Grupos.AsNoTracking()
            .Where(g => g.Estado && idsPeriodos.Contains(g.IdPeriodo))
            .Select(g => new { g.IdGrupo, g.IdPeriodo, g.Identificacion })
            .ToListAsync();

        var idsGrupos = grupos.Select(g => g.IdGrupo).ToList();

        // Cantidad de matrículas habilitadas por grupo
        var inscritos = await _db.Matriculas.AsNoTracking()
            .Where(m => m.Estado && idsGrupos.Contains(m.IdGrupo))
            .GroupBy(m => m.IdGrupo)
            .Select(x => new { IdGrupo = x.Key, Cantidad = x.Count() })
            .ToDictionaryAsync(x => x.IdGrupo, x => x.Cantidad);

        var alertas = new List<object>();

        foreach (var p in periodos)
        {
            var nombrePeriodo = $"{p.Curso} - {p.Modalidad} (período {p.IdPeriodo})";
            var inscripcionVigente = hoy <= p.FechaFinInscripcion;
            var diasRestantes = p.FechaFinInscripcion.DayNumber - hoy.DayNumber;
            var gruposDelPeriodo = grupos.Where(g => g.IdPeriodo == p.IdPeriodo).ToList();

            if (inscripcionVigente)
            {
                foreach (var g in gruposDelPeriodo)
                {
                    var n = inscritos.GetValueOrDefault(g.IdGrupo);

                    // Grupo lleno: se sugiere crear otro grupo
                    if (n >= max)
                        alertas.Add(new
                        {
                            tipo = "GRUPO_LLENO",
                            severidad = "Aviso",
                            idPeriodo = p.IdPeriodo,
                            idGrupo = g.IdGrupo,
                            inscritos = n,
                            mensaje = $"El grupo \"{g.Identificacion}\" de {nombrePeriodo} está lleno ({n}/{max}). Considere crear otro grupo."
                        });

                    // Grupo con pocos estudiantes mientras la inscripción sigue abierta
                    else if (n < min)
                        alertas.Add(new
                        {
                            tipo = "GRUPO_BAJO_MINIMO",
                            severidad = diasRestantes <= DiasUrgente ? "Urgente" : "Aviso",
                            idPeriodo = p.IdPeriodo,
                            idGrupo = g.IdGrupo,
                            inscritos = n,
                            faltan = min - n,
                            diasRestantes,
                            mensaje = $"El grupo \"{g.Identificacion}\" de {nombrePeriodo} tiene {n} de {min} estudiantes mínimos (faltan {min - n}). La inscripción termina en {diasRestantes} día(s)."
                        });
                }
            }
            else
            {
                // La inscripción terminó y el período sigue habilitado: falta cerrarla
                var porCerrar = gruposDelPeriodo.Count(g => inscritos.GetValueOrDefault(g.IdGrupo) < min);
                alertas.Add(new
                {
                    tipo = "PERIODO_PENDIENTE_CIERRE",
                    severidad = "Urgente",
                    idPeriodo = p.IdPeriodo,
                    gruposSinMinimo = porCerrar,
                    mensaje = $"La inscripción de {nombrePeriodo} terminó el {p.FechaFinInscripcion:yyyy-MM-dd} y aún no se ha cerrado. " +
                              $"{porCerrar} grupo(s) no alcanzan el mínimo de {min} estudiantes."
                });
            }
        }

        return Ok(new
        {
            total = alertas.Count,
            alertas,
            mensaje = alertas.Count == 0 ? "No hay alertas pendientes." : null
        });
    }

    // Lee un parámetro numérico; si no existe o no es número, usa el valor por defecto
    private async Task<int> LeerParametroAsync(string clave, int porDefecto)
    {
        var valor = await _db.Parametros.AsNoTracking()
            .Where(p => p.Clave == clave).Select(p => p.Valor).FirstOrDefaultAsync();
        return int.TryParse(valor, out var n) ? n : porDefecto;
    }
}