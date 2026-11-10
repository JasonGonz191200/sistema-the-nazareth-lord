using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Datos que llegan al cambiar el tutor (RF004). IdTutor null = quitar el tutor (solo mayores de edad)
public record CambiarTutorRequest(int? IdTutor);

// Edición de la información específica del rol Estudiante
[ApiController]
[Route("api/estudiantes")]
public class EstudiantesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public EstudiantesController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // Id del usuario que hace la petición (cookie de sesión)
    private int IdUsuarioActual() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Edad cumplida a una fecha
    private static int Edad(DateOnly nacimiento, DateOnly hoy)
    {
        var e = hoy.Year - nacimiento.Year;
        if (nacimiento.AddYears(e) > hoy) e--;
        return e;
    }

    // Carga un estudiante con su persona y su tutor
    private Task<Estudiante?> CargarAsync(int id) =>
        _db.Estudiantes
            .Include(e => e.IdPersonaNavigation)
            .Include(e => e.IdTutorNavigation).ThenInclude(t => t!.IdPersonaNavigation)
            .FirstOrDefaultAsync(e => e.IdEstudiante == id);

    // Formato común de respuesta
    private static object Mapear(Estudiante e, int edad) => new
    {
        e.IdEstudiante,
        e.IdPersona,
        NombreCompleto = e.IdPersonaNavigation.NombreCompleto,
        Carne = e.NumeroCarne,
        Edad = edad,
        Habilitado = e.Estado,
        Tutor = e.IdTutorNavigation == null ? null : new
        {
            e.IdTutorNavigation.IdTutor,
            NombreCompleto = e.IdTutorNavigation.IdPersonaNavigation.NombreCompleto
        }
    };

    // Lee la edad de mayoría desde la tabla parametro (por defecto 18)
    private async Task<int> EdadMayoriaAsync()
    {
        var v = await _db.Parametros.AsNoTracking()
            .Where(p => p.Clave == "EDAD_MAYORIA").Select(p => p.Valor).FirstOrDefaultAsync();
        return int.TryParse(v, out var n) ? n : 18;
    }

    // GET api/estudiantes/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var e = await CargarAsync(id) ?? throw new NegocioException("El estudiante no existe.");
        var edad = Edad(e.IdPersonaNavigation.FechaNacimiento, DateOnly.FromDateTime(DateTime.Today));
        return Ok(Mapear(e, edad));
    }

    // PUT api/estudiantes/{id}/tutor   body: { "idTutor": 1 }
    [HttpPut("{id:int}/tutor")]
    public async Task<IActionResult> CambiarTutor(int id, CambiarTutorRequest r)
    {
        var e = await CargarAsync(id) ?? throw new NegocioException("El estudiante no existe.");

        var hoy = DateOnly.FromDateTime(DateTime.Today);
        var edad = Edad(e.IdPersonaNavigation.FechaNacimiento, hoy);
        var mayoria = await EdadMayoriaAsync();

        // RN001: un estudiante menor de edad siempre necesita tutor
        if (r.IdTutor is null && edad < mayoria)
            throw new NegocioException("RN001: un estudiante menor de edad requiere tutor.");

        if (r.IdTutor == e.IdTutor)
            throw new NegocioException("El estudiante ya tiene ese tutor asignado.");

        Tutor? tutor = null;
        if (r.IdTutor is not null)
        {
            tutor = await _db.Tutors.Include(t => t.IdPersonaNavigation)
                .FirstOrDefaultAsync(t => t.IdTutor == r.IdTutor)
                ?? throw new NegocioException("El tutor indicado no existe.");

            if (!tutor.Estado)
                throw new NegocioException("El tutor seleccionado está inhabilitado.");
            if (tutor.IdPersona == e.IdPersona)
                throw new NegocioException("El tutor no puede ser la misma persona que el estudiante.");
        }

        // Para el registro de auditoría: tutor anterior y nuevo
        var anterior = e.IdTutorNavigation?.IdPersonaNavigation.NombreCompleto ?? "ninguno";
        var nuevo = tutor?.IdPersonaNavigation.NombreCompleto ?? "ninguno";

        // Se actualiza el mismo registro (RN009)
        e.IdTutor = r.IdTutor;
        e.IdTutorNavigation = tutor;
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(IdUsuarioActual(), "EDITAR_ESTUDIANTE", "estudiante", id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"Tutor de \"{e.IdPersonaNavigation.NombreCompleto}\" cambiado de \"{anterior}\" a \"{nuevo}\".");

        return Ok(Mapear(e, edad));
    }
}