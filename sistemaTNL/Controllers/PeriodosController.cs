using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Cuerpo para crear o editar un período (RF017 y RF018).
// Las fechas se envían como texto "2026-12-01".
public record GuardarPeriodoRequest(
    int IdCurso,
    int IdModalidad,
    DateOnly FechaInicioInscripcion,
    DateOnly FechaFinInscripcion,
    DateOnly FechaInicioCurso,
    DateOnly FechaFinCurso);

[ApiController]
[Route("api/periodos")]
public class PeriodosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public PeriodosController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // GET api/periodos
    // Lista todos los períodos con el nombre de su curso y modalidad
    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await _db.PeriodoInscripcions
            .AsNoTracking()
            .OrderByDescending(p => p.FechaInicioCurso)
            .Select(p => new
            {
                p.IdPeriodo,
                p.IdCurso,
                Curso = p.IdCursoNavigation.Nombre,
                p.IdModalidad,
                Modalidad = p.IdModalidadNavigation.Nombre,
                p.FechaCreacion,
                p.FechaInicioInscripcion,
                p.FechaFinInscripcion,
                p.FechaInicioCurso,
                p.FechaFinCurso,
                p.Estado
            })
            .ToListAsync());

    // POST api/periodos  (RF017)
    [HttpPost]
    public async Task<IActionResult> Crear(GuardarPeriodoRequest req)
    {
        // La fecha de creación del período es hoy (así la define la base)
        var hoy = DateOnly.FromDateTime(DateTime.Today);
        ValidarFechas(req, hoy);
        await ValidarCursoYModalidadAsync(req.IdCurso, req.IdModalidad);

        var periodo = new PeriodoInscripcion
        {
            IdCurso = req.IdCurso,
            IdModalidad = req.IdModalidad,
            FechaInicioInscripcion = req.FechaInicioInscripcion,
            FechaFinInscripcion = req.FechaFinInscripcion,
            FechaInicioCurso = req.FechaInicioCurso,
            FechaFinCurso = req.FechaFinCurso,
            Estado = "Habilitado"          // estado inicial (RN012)
        };
        _db.PeriodoInscripcions.Add(periodo);
        await _db.SaveChangesAsync();

        await AuditarAsync("CREAR_PERIODO", periodo.IdPeriodo, "Período creado");

        return Created("/api/periodos", Resumen(periodo));
    }

    // PUT api/periodos/{id}  (RF018)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Editar(int id, GuardarPeriodoRequest req)
    {
        var periodo = await _db.PeriodoInscripcions.FirstOrDefaultAsync(p => p.IdPeriodo == id)
            ?? throw new NegocioException("El período no existe.");

        // RN012: un período Finalizado conserva su información y no se modifica
        if (periodo.Estado == "Finalizado")
            throw new NegocioException("RN012: un período Finalizado no puede modificarse.");

        // En la edición, las fechas se comparan con la fecha de creación original (RN019)
        ValidarFechas(req, periodo.FechaCreacion);

        // Solo validamos curso y modalidad si el usuario los cambió
        var cambiaCursoOModalidad = periodo.IdCurso != req.IdCurso || periodo.IdModalidad != req.IdModalidad;
        if (cambiaCursoOModalidad)
        {
            await ValidarCursoYModalidadAsync(req.IdCurso, req.IdModalidad);

            // Protección: si ya hay grupos, cambiar el curso o la modalidad dejaría
            // docentes y grupos inconsistentes con el período
            if (await _db.Grupos.AnyAsync(g => g.IdPeriodo == id))
                throw new NegocioException(
                    "El período ya tiene grupos; no se puede cambiar su curso ni su modalidad.");
        }

        periodo.IdCurso = req.IdCurso;
        periodo.IdModalidad = req.IdModalidad;
        periodo.FechaInicioInscripcion = req.FechaInicioInscripcion;
        periodo.FechaFinInscripcion = req.FechaFinInscripcion;
        periodo.FechaInicioCurso = req.FechaInicioCurso;
        periodo.FechaFinCurso = req.FechaFinCurso;

        await _db.SaveChangesAsync();

        await AuditarAsync("EDITAR_PERIODO", periodo.IdPeriodo, "Período editado");

        return Ok(Resumen(periodo));
    }

    // Valida el orden de las fechas con mensajes claros (la base también lo exige)
    private static void ValidarFechas(GuardarPeriodoRequest r, DateOnly fechaCreacion)
    {
        if (r.FechaFinInscripcion < r.FechaInicioInscripcion)
            throw new NegocioException("La fecha de fin de inscripción no puede ser anterior a la de inicio.");
        if (r.FechaFinCurso < r.FechaInicioCurso)
            throw new NegocioException("La fecha de fin del curso no puede ser anterior a la de inicio.");

        // RN019: ninguna fecha puede ser anterior a la fecha de creación del período
        if (r.FechaInicioInscripcion < fechaCreacion || r.FechaFinInscripcion < fechaCreacion ||
            r.FechaInicioCurso < fechaCreacion || r.FechaFinCurso < fechaCreacion)
            throw new NegocioException(
                $"RN019: ninguna fecha puede ser anterior a la fecha de creación del período ({fechaCreacion:yyyy-MM-dd}).");
    }

    // El curso y la modalidad deben existir y estar habilitados (RN018)
    private async Task ValidarCursoYModalidadAsync(int idCurso, int idModalidad)
    {
        if (!await _db.Cursos.AnyAsync(c => c.IdCurso == idCurso && c.Estado))
            throw new NegocioException("El curso seleccionado no existe o está inhabilitado.");
        if (!await _db.Modalidads.AnyAsync(m => m.IdModalidad == idModalidad && m.Estado))
            throw new NegocioException("La modalidad seleccionada no existe o está inhabilitada.");
    }

    // Auditoría: quién hizo el cambio y desde qué IP
    private async Task AuditarAsync(string accion, int idPeriodo, string descripcion)
    {
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auditoria.RegistrarAsync(idUsuario, accion, "periodo_inscripcion", idPeriodo,
            HttpContext.Connection.RemoteIpAddress?.ToString(), descripcion);
    }

    // Respuesta compacta con los datos del período
    private static object Resumen(PeriodoInscripcion p) => new
    {
        p.IdPeriodo,
        p.IdCurso,
        p.IdModalidad,
        p.FechaCreacion,
        p.FechaInicioInscripcion,
        p.FechaFinInscripcion,
        p.FechaInicioCurso,
        p.FechaFinCurso,
        p.Estado
    };
}