using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Datos que llegan al editar un docente (RF014)
public record ActualizarDocenteRequest(
    bool EsLicenciado,
    int AniosExperiencia,
    string? InstitucionesLaborales,
    List<int> Modalidades);      // ids de las modalidades en las que puede dar clases (disponibilidad)

// Edición de la información específica del rol Docente
[ApiController]
[Route("api/docentes")]
public class DocentesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public DocentesController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // Id del usuario que hace la petición (cookie de sesión)
    private int IdUsuarioActual() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Formato común de respuesta
    private static object Mapear(Docente d) => new
    {
        d.IdDocente,
        d.IdPersona,
        NombreCompleto = d.IdPersonaNavigation.NombreCompleto,
        Habilitado = d.Estado,
        d.EsLicenciado,
        d.AniosExperiencia,
        d.InstitucionesLaborales,
        Modalidades = d.IdModalidads
            .OrderBy(m => m.Nombre)
            .Select(m => new { m.IdModalidad, m.Nombre })
            .ToList()
    };

    // GET api/docentes?buscar=carlos   → lista de docentes (para encontrar el idDocente)
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] string? buscar)
    {
        var q = _db.Docentes.AsNoTracking()
            .Include(d => d.IdPersonaNavigation)
            .Include(d => d.IdModalidads)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var b = buscar.Trim();
            q = q.Where(d => d.IdPersonaNavigation.NombreCompleto.Contains(b));
        }

        var docentes = await q.OrderBy(d => d.IdPersonaNavigation.NombreCompleto).ToListAsync();
        return Ok(docentes.Select(Mapear));
    }

    // GET api/docentes/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var d = await _db.Docentes.AsNoTracking()
            .Include(x => x.IdPersonaNavigation)
            .Include(x => x.IdModalidads)
            .FirstOrDefaultAsync(x => x.IdDocente == id)
            ?? throw new NegocioException("El docente no existe.");
        return Ok(Mapear(d));
    }

    // PUT api/docentes/{id}   → edita los datos específicos del docente (RF014)
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, ActualizarDocenteRequest r)
    {
        // Validaciones de los campos
        if (r.AniosExperiencia < 0 || r.AniosExperiencia > 80)
            throw new NegocioException("Los años de experiencia deben estar entre 0 y 80.");

        var instituciones = string.IsNullOrWhiteSpace(r.InstitucionesLaborales)
            ? null : r.InstitucionesLaborales.Trim();
        if (instituciones is not null && instituciones.Length > 500)
            throw new NegocioException("Las instituciones laborales no pueden superar los 500 caracteres.");

        var idsNuevos = (r.Modalidades ?? new List<int>()).Distinct().ToList();
        if (idsNuevos.Count == 0)
            throw new NegocioException("El docente debe tener disponibilidad en al menos una modalidad.");

        // Se carga el docente con sus modalidades actuales
        var docente = await _db.Docentes
            .Include(d => d.IdPersonaNavigation)
            .Include(d => d.IdModalidads)
            .FirstOrDefaultAsync(d => d.IdDocente == id)
            ?? throw new NegocioException("El docente no existe.");

        // Las modalidades indicadas deben existir
        var modalidades = await _db.Modalidads.Where(m => idsNuevos.Contains(m.IdModalidad)).ToListAsync();
        if (modalidades.Count != idsNuevos.Count)
            throw new NegocioException("Alguna de las modalidades indicadas no existe.");

        var idsActuales = docente.IdModalidads.Select(m => m.IdModalidad).ToList();
        var idsAgregar = idsNuevos.Except(idsActuales).ToList();
        var idsQuitar = idsActuales.Except(idsNuevos).ToList();

        // Las modalidades que se agregan deben estar habilitadas
        var inhabilitada = modalidades.FirstOrDefault(m => idsAgregar.Contains(m.IdModalidad) && !m.Estado);
        if (inhabilitada is not null)
            throw new NegocioException($"La modalidad \"{inhabilitada.Nombre}\" está inhabilitada.");

        // RN005: no se puede quitar una modalidad en la que el docente tiene un grupo activo
        if (idsQuitar.Count > 0)
        {
            var conflictos = await _db.Grupos.AsNoTracking()
                .Where(g => g.IdDocente == id && g.Estado
                            && g.IdPeriodoNavigation.Estado == "Habilitado"
                            && idsQuitar.Contains(g.IdPeriodoNavigation.IdModalidad))
                .Select(g => g.Identificacion + " (" + g.IdPeriodoNavigation.IdModalidadNavigation.Nombre + ")")
                .ToListAsync();

            if (conflictos.Count > 0)
                throw new NegocioException(
                    "No se puede quitar la disponibilidad porque el docente tiene grupos activos en esa modalidad: " +
                    string.Join(", ", conflictos) + ". Reemplace al docente en esos grupos primero.");
        }

        // Se aplican los cambios sobre el mismo registro (RN009: no se elimina ni se duplica)
        docente.EsLicenciado = r.EsLicenciado;
        docente.AniosExperiencia = r.AniosExperiencia;
        docente.InstitucionesLaborales = instituciones;

        foreach (var m in docente.IdModalidads.Where(m => idsQuitar.Contains(m.IdModalidad)).ToList())
            docente.IdModalidads.Remove(m);
        foreach (var m in modalidades.Where(m => idsAgregar.Contains(m.IdModalidad)))
            docente.IdModalidads.Add(m);

        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(IdUsuarioActual(), "EDITAR_DOCENTE", "docente", id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"Docente \"{docente.IdPersonaNavigation.NombreCompleto}\" actualizado. " +
            $"Licenciado: {(r.EsLicenciado ? "Sí" : "No")}, años: {r.AniosExperiencia}, " +
            $"modalidades: {string.Join(",", idsNuevos.OrderBy(x => x))}.");

        return Ok(Mapear(docente));
    }
}