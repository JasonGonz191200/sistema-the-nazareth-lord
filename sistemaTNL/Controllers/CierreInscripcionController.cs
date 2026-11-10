using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Cuerpo del cierre: obliga a confirmar de forma explícita
public record CerrarInscripcionRequest(bool Confirmar);

// Cierre del proceso de inscripción de un período (RN012 / RN014 / RN031)
[ApiController]
[Route("api/periodos/{idPeriodo:int}/cierre-inscripcion")]
public class CierreInscripcionController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public CierreInscripcionController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // Id del usuario que hace la petición (cookie de sesión)
    private int IdUsuarioActual() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // GET api/periodos/{idPeriodo}/cierre-inscripcion
    // Vista previa: NO modifica nada
    [HttpGet]
    public async Task<IActionResult> VistaPrevia(int idPeriodo)
    {
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var periodo = await _db.PeriodoInscripcions.AsNoTracking()
            .Where(p => p.IdPeriodo == idPeriodo)
            .Select(p => new
            {
                p.IdPeriodo,
                Curso = p.IdCursoNavigation.Nombre,
                Modalidad = p.IdModalidadNavigation.Nombre,
                p.Estado,
                p.FechaFinInscripcion
            })
            .FirstOrDefaultAsync()
            ?? throw new NegocioException("El período no existe.");

        var min = await LeerParametroAsync("MIN_ESTUDIANTES_GRUPO", 8);

        // Grupos habilitados del período
        var grupos = await _db.Grupos.AsNoTracking()
            .Where(g => g.IdPeriodo == idPeriodo && g.Estado)
            .Select(g => new { g.IdGrupo, g.Identificacion })
            .ToListAsync();
        var idsGrupos = grupos.Select(g => g.IdGrupo).ToList();

        // Matrículas habilitadas por grupo
        var inscritos = await _db.Matriculas.AsNoTracking()
            .Where(m => m.Estado && idsGrupos.Contains(m.IdGrupo))
            .GroupBy(m => m.IdGrupo)
            .Select(x => new { IdGrupo = x.Key, Cantidad = x.Count() })
            .ToDictionaryAsync(x => x.IdGrupo, x => x.Cantidad);

        var detalle = grupos.Select(g =>
        {
            var n = inscritos.GetValueOrDefault(g.IdGrupo);
            return new
            {
                g.IdGrupo,
                g.Identificacion,
                inscritos = n,
                seCerraria = n < min   // se inhabilita el grupo y sus matrículas
            };
        }).ToList();

        var sinMinimo = detalle.Where(d => d.seCerraria).ToList();

        // ¿Se puede cerrar ahora? Lo mismo que valida el procedimiento
        string? motivoNoPuede = null;
        if (periodo.Estado != "Habilitado")
            motivoNoPuede = "El período no está habilitado.";
        else if (hoy <= periodo.FechaFinInscripcion)
            motivoNoPuede = $"La inscripción termina el {periodo.FechaFinInscripcion:yyyy-MM-dd}; solo se puede cerrar después de esa fecha.";

        return Ok(new
        {
            periodo.IdPeriodo,
            periodo.Curso,
            periodo.Modalidad,
            periodo.Estado,
            periodo.FechaFinInscripcion,
            minimoPorGrupo = min,
            puedeCerrar = motivoNoPuede is null,
            motivoNoPuedeCerrar = motivoNoPuede,
            grupos = detalle,
            gruposQueSeCerrarian = sinMinimo.Count,
            matriculasQueSeInhabilitarian = sinMinimo.Sum(d => d.inscritos),
            elPeriodoQuedariaInhabilitado = detalle.All(d => d.seCerraria),
            advertencia = "El cierre es irreversible: una matrícula inhabilitada no se rehabilita. " +
                          "Además, los estudiantes que debían continuar desde el período anterior y no tengan " +
                          "matrícula vigente pasan a inhabilitados (RN014)."
        });
    }

    // POST api/periodos/{idPeriodo}/cierre-inscripcion   body: { "confirmar": true }
    [HttpPost]
    public async Task<IActionResult> Cerrar(int idPeriodo, CerrarInscripcionRequest r)
    {
        if (!r.Confirmar)
            throw new NegocioException("Debe confirmar el cierre enviando \"confirmar\": true. Revise antes la vista previa.");

        if (!await _db.PeriodoInscripcions.AnyAsync(p => p.IdPeriodo == idPeriodo))
            throw new NegocioException("El período no existe.");

        // Guardamos cuántas matrículas habilitadas había antes para informar el resultado
        var antes = await _db.Matriculas.CountAsync(m => m.IdPeriodo == idPeriodo && m.Estado);

        // El procedimiento valida y ejecuta todo en una sola transacción;
        // si algo falla lanza un error 500xx que el middleware convierte en 400
        await _db.Database.ExecuteSqlInterpolatedAsync($"EXEC dbo.sp_cerrar_inscripcion {idPeriodo}");

        var despues = await _db.Matriculas.CountAsync(m => m.IdPeriodo == idPeriodo && m.Estado);
        var estadoPeriodo = await _db.PeriodoInscripcions.AsNoTracking()
            .Where(p => p.IdPeriodo == idPeriodo).Select(p => p.Estado).FirstAsync();

        await _auditoria.RegistrarAsync(IdUsuarioActual(), "CERRAR_INSCRIPCION", "periodo_inscripcion", idPeriodo,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"Inscripción cerrada. Matrículas inhabilitadas: {antes - despues}. Estado final del período: {estadoPeriodo}.");

        return Ok(new
        {
            idPeriodo,
            matriculasInhabilitadas = antes - despues,
            estadoPeriodo,
            mensaje = "La inscripción del período se cerró correctamente."
        });
    }

    // Lee un parámetro numérico de la tabla parametro
    private async Task<int> LeerParametroAsync(string clave, int porDefecto)
    {
        var valor = await _db.Parametros.AsNoTracking()
            .Where(p => p.Clave == clave).Select(p => p.Valor).FirstOrDefaultAsync();
        return int.TryParse(valor, out var n) ? n : porDefecto;
    }
}