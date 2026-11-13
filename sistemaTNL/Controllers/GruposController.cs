using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Cuerpo para crear o editar un grupo (RF019 y RF020).
// El horario es opcional: si no se envía, la base usa el de la modalidad.
public record GuardarGrupoRequest(
    int IdPeriodo,
    int IdDocente,
    int IdAula,
    int IdNivel,
    string Identificacion,
    string? Horario);

// Cuerpo para habilitar (true) o inhabilitar (false) un grupo (RF021 y RF022)
public record CambiarEstadoGrupoRequest(bool Habilitado);

// Forma en que devolvemos cada grupo en las respuestas
public record GrupoItem(
    int IdGrupo, int IdPeriodo, string Curso, string Modalidad,
    int IdDocente, string Docente, int IdAula, string Aula,
    int IdNivel, string Nivel, string Identificacion, string? Horario,
    bool Estado, int Inscritos);

[ApiController]
[Route("api/grupos")]
public class GruposController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public GruposController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // GET api/grupos/docentes?idPeriodo=1
    // Docentes habilitados con disponibilidad en la modalidad del período (RN005)
    [HttpGet("docentes")]
    public async Task<IActionResult> DocentesDisponibles([FromQuery] int idPeriodo)
    {
        // La modalidad se obtiene del período
        var idModalidad = await _db.PeriodoInscripcions
            .Where(p => p.IdPeriodo == idPeriodo)
            .Select(p => (int?)p.IdModalidad)
            .FirstOrDefaultAsync()
            ?? throw new NegocioException("El período no existe.");

        var docentes = await _db.Docentes
            .AsNoTracking()
            .Where(d => d.Estado && d.IdModalidads.Any(m => m.IdModalidad == idModalidad))
            .OrderBy(d => d.IdPersonaNavigation.NombreCompleto)
            .Select(d => new { d.IdDocente, Nombre = d.IdPersonaNavigation.NombreCompleto })
            .ToListAsync();

        return Ok(docentes);
    }

    // GET api/grupos            -> todos los grupos
    // GET api/grupos?idPeriodo=1 -> solo los de un período
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] int? idPeriodo)
    {
        var q = _db.Grupos.AsNoTracking().AsQueryable();
        if (idPeriodo is not null) q = q.Where(g => g.IdPeriodo == idPeriodo);
        // Se ordena ANTES de proyectar: EF no puede traducir el orden sobre el resultado ya armado
        return Ok(await Proyectar(q.OrderBy(g => g.Identificacion)).ToListAsync());
    }

    // POST api/grupos  (RF019)
    [HttpPost]
    public async Task<IActionResult> Crear(GuardarGrupoRequest req)
    {
        var identificacion = req.Identificacion?.Trim();
        if (string.IsNullOrEmpty(identificacion))
            throw new NegocioException("La identificación del grupo es obligatoria.");

        // El período debe existir y estar habilitado
        var periodo = await _db.PeriodoInscripcions.FirstOrDefaultAsync(p => p.IdPeriodo == req.IdPeriodo)
            ?? throw new NegocioException("El período seleccionado no existe.");
        if (periodo.Estado != "Habilitado")
            throw new NegocioException("El período del grupo debe estar habilitado.");

        await ValidarDocenteAsync(req.IdDocente, periodo.IdModalidad);
        await ValidarAulaAsync(req.IdAula);
        await ValidarNivelAsync(req.IdNivel);
        await ValidarIdentificacionAsync(req.IdPeriodo, identificacion, null);

        var grupo = new Grupo
        {
            IdPeriodo = req.IdPeriodo,
            IdDocente = req.IdDocente,
            IdAula = req.IdAula,
            IdNivel = req.IdNivel,
            Identificacion = identificacion,
            Horario = string.IsNullOrWhiteSpace(req.Horario) ? null : req.Horario.Trim(),
            Estado = true
        };
        _db.Grupos.Add(grupo);
        await _db.SaveChangesAsync();

        await AuditarAsync("CREAR_GRUPO", grupo.IdGrupo, identificacion);

        // Volvemos a leer el grupo: el trigger pudo completar el horario con el de la modalidad
        return Created("/api/grupos", await ObtenerAsync(grupo.IdGrupo));
    }

    // PUT api/grupos/{id}  (RF020)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Editar(int id, GuardarGrupoRequest req)
    {
        var grupo = await _db.Grupos.FirstOrDefaultAsync(g => g.IdGrupo == id)
            ?? throw new NegocioException("El grupo no existe.");

        var identificacion = req.Identificacion?.Trim();
        if (string.IsNullOrEmpty(identificacion))
            throw new NegocioException("La identificación del grupo es obligatoria.");

        // Período al que quedará asociado el grupo (puede ser el mismo)
        var periodo = await _db.PeriodoInscripcions.FirstOrDefaultAsync(p => p.IdPeriodo == req.IdPeriodo)
            ?? throw new NegocioException("El período seleccionado no existe.");

        if (grupo.IdPeriodo != req.IdPeriodo)
        {
            if (periodo.Estado != "Habilitado")
                throw new NegocioException("El período del grupo debe estar habilitado.");

            // Un grupo con matrículas no puede cambiar de período
            if (await _db.Matriculas.AnyAsync(m => m.IdGrupo == id))
                throw new NegocioException("El grupo ya tiene matrículas y no puede cambiar de período.");
        }

        // Validamos docente y aula solo si cambiaron (igual que hace el trigger)
        if (grupo.IdDocente != req.IdDocente || grupo.IdPeriodo != req.IdPeriodo)
            await ValidarDocenteAsync(req.IdDocente, periodo.IdModalidad);
        if (grupo.IdAula != req.IdAula)
            await ValidarAulaAsync(req.IdAula);
        if (grupo.IdNivel != req.IdNivel)
            await ValidarNivelAsync(req.IdNivel);
        await ValidarIdentificacionAsync(req.IdPeriodo, identificacion, id);

        grupo.IdPeriodo = req.IdPeriodo;
        grupo.IdDocente = req.IdDocente;
        grupo.IdAula = req.IdAula;
        grupo.IdNivel = req.IdNivel;
        grupo.Identificacion = identificacion;
        grupo.Horario = string.IsNullOrWhiteSpace(req.Horario) ? null : req.Horario.Trim();

        await _db.SaveChangesAsync();

        await AuditarAsync("EDITAR_GRUPO", grupo.IdGrupo, identificacion);

        return Ok(await ObtenerAsync(grupo.IdGrupo));
    }

    // PUT api/grupos/{id}/estado  (RF021 y RF022)
    // Ejemplo: PUT api/grupos/1/estado con { "habilitado": false }
    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoGrupoRequest req)
    {
        var grupo = await _db.Grupos.FirstOrDefaultAsync(g => g.IdGrupo == id)
            ?? throw new NegocioException("El grupo no existe.");

        // Evitamos "cambiar" a un estado en el que ya está
        if (grupo.Estado == req.Habilitado)
            throw new NegocioException(
                $"El grupo ya está {(req.Habilitado ? "habilitado" : "inhabilitado")}.");

        grupo.Estado = req.Habilitado;

        // RN020: el trigger rechaza inhabilitar un grupo con estudiantes matriculados;
        // el middleware lo convierte en un 400 con ese mensaje.
        await _db.SaveChangesAsync();

        await AuditarAsync(req.Habilitado ? "HABILITAR_GRUPO" : "INHABILITAR_GRUPO",
            grupo.IdGrupo, grupo.Identificacion);

        return Ok(await ObtenerAsync(grupo.IdGrupo));
    }

    // ---------- Apoyo ----------

    // El docente debe estar habilitado y tener disponibilidad en la modalidad (RN005)
    private async Task ValidarDocenteAsync(int idDocente, int idModalidad)
    {
        var existe = await _db.Docentes.AnyAsync(d => d.IdDocente == idDocente);
        if (!existe) throw new NegocioException("El docente seleccionado no existe.");

        var disponible = await _db.Docentes.AnyAsync(d =>
            d.IdDocente == idDocente && d.Estado &&
            d.IdModalidads.Any(m => m.IdModalidad == idModalidad));
        if (!disponible)
            throw new NegocioException(
                "RN005: el docente no está habilitado o no tiene disponibilidad para la modalidad del período.");
    }

    // El aula debe existir y estar habilitada
    private async Task ValidarAulaAsync(int idAula)
    {
        if (!await _db.Aulas.AnyAsync(a => a.IdAula == idAula && a.Estado))
            throw new NegocioException("El aula seleccionada no existe o no está disponible.");
    }

    // El nivel debe existir y estar habilitado
    private async Task ValidarNivelAsync(int idNivel)
    {
        if (!await _db.Nivels.AnyAsync(n => n.IdNivel == idNivel && n.Estado))
            throw new NegocioException("El nivel seleccionado no existe o está inhabilitado.");
    }

    // La identificación no se puede repetir dentro de un mismo período
    private async Task ValidarIdentificacionAsync(int idPeriodo, string identificacion, int? idGrupoActual)
    {
        if (await _db.Grupos.AnyAsync(g =>
                g.IdPeriodo == idPeriodo && g.Identificacion == identificacion && g.IdGrupo != idGrupoActual))
            throw new NegocioException("Ya existe un grupo con esa identificación en el período.");
    }

    // Lee un grupo con sus nombres ya resueltos
    private async Task<GrupoItem?> ObtenerAsync(int idGrupo) =>
        await Proyectar(_db.Grupos.AsNoTracking().Where(g => g.IdGrupo == idGrupo)).FirstOrDefaultAsync();

    // Convierte grupos en GrupoItem, trayendo curso, modalidad, docente, aula y nivel
    private static IQueryable<GrupoItem> Proyectar(IQueryable<Grupo> q) =>
        q.Select(g => new GrupoItem(
            g.IdGrupo,
            g.IdPeriodo,
            g.IdPeriodoNavigation.IdCursoNavigation.Nombre,
            g.IdPeriodoNavigation.IdModalidadNavigation.Nombre,
            g.IdDocente,
            g.IdDocenteNavigation.IdPersonaNavigation.NombreCompleto,
            g.IdAula,
            g.IdAulaNavigation.Nombre,
            g.IdNivel,
            g.IdNivelNavigation.Nombre,
            g.Identificacion,
            g.Horario,
            g.Estado,
            g.Matriculas.Count(m => m.Estado)));

    // Auditoría: quién hizo el cambio y desde qué IP
    private async Task AuditarAsync(string accion, int idGrupo, string descripcion)
    {
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auditoria.RegistrarAsync(idUsuario, accion, "grupo", idGrupo,
            HttpContext.Connection.RemoteIpAddress?.ToString(), descripcion);
    }
}