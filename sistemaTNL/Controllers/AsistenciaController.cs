using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Asistencia de un estudiante (por su matrícula) en una clase. Estado: "Presente" o "Ausente".
public record DetalleAsistenciaRequest(int IdMatricula, string Estado);

// Cuerpo para registrar la asistencia de un grupo en una fecha (RF036)
public record RegistrarAsistenciaRequest(
    int IdGrupo,
    DateOnly FechaClase,
    List<DetalleAsistenciaRequest> Detalles);

[ApiController]
[Route("api/asistencia")]
public class AsistenciaController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public AsistenciaController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // GET api/asistencia/lista?idGrupo=1&fecha=2026-11-08
    // Estudiantes que deben aparecer en la lista de asistencia de esa clase (RN034 y RN035)
    [HttpGet("lista")]
    public async Task<IActionResult> Lista([FromQuery] int idGrupo, [FromQuery] DateOnly fecha)
    {
        await ValidarClaseAsync(idGrupo, fecha);

        var estudiantes = await Elegibles(idGrupo, fecha)
            .AsNoTracking()
            .OrderBy(m => m.IdEstudianteNavigation.IdPersonaNavigation.NombreCompleto)
            .Select(m => new
            {
                m.IdMatricula,
                m.IdEstudiante,
                m.IdEstudianteNavigation.NumeroCarne,
                Nombre = m.IdEstudianteNavigation.IdPersonaNavigation.NombreCompleto
            })
            .ToListAsync();

        // Avisamos si esa clase ya tiene asistencia guardada
        var yaRegistrada = await _db.RegistroAsistencia
            .AnyAsync(r => r.IdGrupo == idGrupo && r.FechaClase == fecha);

        return Ok(new { idGrupo, fecha, yaRegistrada, estudiantes });
    }

    // GET api/asistencia?idGrupo=1
    // Clases con asistencia registrada de un grupo
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int idGrupo) =>
        Ok(await _db.RegistroAsistencia.AsNoTracking()
            .Where(r => r.IdGrupo == idGrupo)
            .OrderByDescending(r => r.FechaClase)
            .Select(r => new
            {
                r.IdRegistro,
                r.FechaClase,
                r.FechaRegistro,
                Presentes = r.DetalleAsistencia.Count(d => d.Estado == "Presente"),
                Ausentes = r.DetalleAsistencia.Count(d => d.Estado == "Ausente")
            })
            .ToListAsync());

    // GET api/asistencia/{id}
    // Una clase con la asistencia de cada estudiante
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var detalle = await DetalleAsync(id);
        return detalle is null ? NotFound(new { mensaje = "El registro de asistencia no existe." }) : Ok(detalle);
    }

    // GET api/asistencia/estudiante/{idEstudiante}
    // Historial de asistencia de un estudiante (se conserva aunque su matrícula se inhabilite)
    [HttpGet("estudiante/{idEstudiante:int}")]
    public async Task<IActionResult> Historial(int idEstudiante) =>
        Ok(await _db.DetalleAsistencia.AsNoTracking()
            .Where(d => d.IdMatriculaNavigation.IdEstudiante == idEstudiante)
            .OrderByDescending(d => d.IdRegistroNavigation.FechaClase)
            .Select(d => new
            {
                d.IdRegistroNavigation.FechaClase,
                Grupo = d.IdRegistroNavigation.IdGrupoNavigation.Identificacion,
                d.IdMatricula,
                d.Estado
            })
            .ToListAsync());

    // POST api/asistencia  (RF036)
    [HttpPost]
    public async Task<IActionResult> Registrar(RegistrarAsistenciaRequest req)
    {
        // La clase debe existir dentro de las fechas del curso y ya haberse impartido
        await ValidarClaseAsync(req.IdGrupo, req.FechaClase);

        // RN032: un solo registro por grupo y fecha
        if (await _db.RegistroAsistencia.AnyAsync(r => r.IdGrupo == req.IdGrupo && r.FechaClase == req.FechaClase))
            throw new NegocioException("RN032: ya existe un registro de asistencia para ese grupo y esa fecha.");

        // Estudiantes que corresponden a esa clase
        var elegibles = await Elegibles(req.IdGrupo, req.FechaClase)
            .AsNoTracking()
            .Select(m => m.IdMatricula)
            .ToListAsync();
        if (elegibles.Count == 0)
            throw new NegocioException("No hay estudiantes disponibles para registrar asistencia en esa fecha.");

        if (req.Detalles is null || req.Detalles.Count == 0)
            throw new NegocioException("Debe asignar el estado de asistencia a los estudiantes.");

        // Normalizamos el estado: aceptamos "presente", "PRESENTE", etc.
        var detalles = new List<DetalleAsistencium>();
        foreach (var d in req.Detalles)
        {
            var estado = (d.Estado ?? "").Trim().ToLowerInvariant() switch
            {
                "presente" => "Presente",
                "ausente" => "Ausente",
                _ => throw new NegocioException("El estado de asistencia debe ser \"Presente\" o \"Ausente\".")
            };
            detalles.Add(new DetalleAsistencium { IdMatricula = d.IdMatricula, Estado = estado });
        }

        // RN034: no se repiten estudiantes ni se aceptan los que no están en la lista
        if (detalles.Select(d => d.IdMatricula).Distinct().Count() != detalles.Count)
            throw new NegocioException("Un estudiante no puede aparecer dos veces en la misma lista.");
        if (detalles.Any(d => !elegibles.Contains(d.IdMatricula)))
            throw new NegocioException(
                "RN034: hay estudiantes que no pertenecen a la lista de asistencia de ese grupo y esa fecha.");

        // Todos los estudiantes de la lista deben tener un estado asignado
        if (detalles.Count != elegibles.Count)
            throw new NegocioException(
                $"Falta asignar el estado a {elegibles.Count - detalles.Count} estudiante(s) de la lista.");

        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var registro = new RegistroAsistencium
        {
            IdGrupo = req.IdGrupo,
            IdUsuario = idUsuario,
            FechaClase = req.FechaClase,
            DetalleAsistencia = detalles
        };
        _db.RegistroAsistencia.Add(registro);

        // Registro y detalles se guardan juntos; los triggers de la base lo vuelven inmutable (RN033)
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(idUsuario, "REGISTRAR_ASISTENCIA", "registro_asistencia",
            registro.IdRegistro, HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"Grupo {req.IdGrupo}, clase del {req.FechaClase:yyyy-MM-dd}");

        return Created($"/api/asistencia/{registro.IdRegistro}", await DetalleAsync(registro.IdRegistro));
    }

    // ---------- Apoyo ----------

    // Matrículas que cuentan para la lista de una clase: ya matriculadas en esa fecha y
    // que no se hayan inhabilitado antes de ella (misma condición que el trigger de la base)
    private IQueryable<Matricula> Elegibles(int idGrupo, DateOnly fecha) =>
        _db.Matriculas.Where(m =>
            m.IdGrupo == idGrupo &&
            m.FechaMatricula <= fecha &&
            (m.Estado || (m.FechaInhabilitacion != null && m.FechaInhabilitacion >= fecha)));

    // El grupo debe existir (aunque esté inhabilitado) y la fecha debe ser una clase
    // ya impartida dentro de las fechas del curso (RN036)
    private async Task ValidarClaseAsync(int idGrupo, DateOnly fecha)
    {
        var grupo = await _db.Grupos.AsNoTracking()
            .Where(g => g.IdGrupo == idGrupo)
            .Select(g => new
            {
                g.IdPeriodoNavigation.FechaInicioCurso,
                g.IdPeriodoNavigation.FechaFinCurso
            })
            .FirstOrDefaultAsync()
            ?? throw new NegocioException("El grupo no existe.");

        if (fecha > DateOnly.FromDateTime(DateTime.Today))
            throw new NegocioException("RN036: la clase aún no se ha impartido; la fecha no puede ser futura.");
        if (fecha < grupo.FechaInicioCurso || fecha > grupo.FechaFinCurso)
            throw new NegocioException(
                $"La fecha está fuera de las fechas del curso ({grupo.FechaInicioCurso:yyyy-MM-dd} a {grupo.FechaFinCurso:yyyy-MM-dd}).");
    }

    // Una clase con la asistencia de cada estudiante
    private async Task<object?> DetalleAsync(int idRegistro)
    {
        var registro = await _db.RegistroAsistencia.AsNoTracking()
            .Where(r => r.IdRegistro == idRegistro)
            .Select(r => new
            {
                r.IdRegistro,
                r.IdGrupo,
                Grupo = r.IdGrupoNavigation.Identificacion,
                r.FechaClase,
                r.FechaRegistro,
                RegistradoPor = r.IdUsuarioNavigation.NombreUsuario
            })
            .FirstOrDefaultAsync();
        if (registro is null) return null;

        var estudiantes = await _db.DetalleAsistencia.AsNoTracking()
            .Where(d => d.IdRegistro == idRegistro)
            .OrderBy(d => d.IdMatriculaNavigation.IdEstudianteNavigation.IdPersonaNavigation.NombreCompleto)
            .Select(d => new
            {
                d.IdMatricula,
                d.IdMatriculaNavigation.IdEstudianteNavigation.NumeroCarne,
                Nombre = d.IdMatriculaNavigation.IdEstudianteNavigation.IdPersonaNavigation.NombreCompleto,
                d.Estado
            })
            .ToListAsync();

        return new
        {
            registro,
            presentes = estudiantes.Count(e => e.Estado == "Presente"),
            ausentes = estudiantes.Count(e => e.Estado == "Ausente"),
            estudiantes
        };
    }
}