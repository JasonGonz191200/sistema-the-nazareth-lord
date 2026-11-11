using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Datos que llegan al editar un curso (RF024)
public record EditarCursoRequest(string Nombre, int? IdNivel);

// Edición de cursos. Comparte la ruta base "api/cursos" con CursosController.
[ApiController]
[Route("api/cursos")]
public class CursosEdicionController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public CursosEdicionController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // Id del usuario que hace la petición (cookie de sesión)
    private int IdUsuarioActual() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // PUT api/cursos/{id}   body: { "nombre": "Inglés General", "idNivel": 1 }
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Editar(int id, EditarCursoRequest r)
    {
        // Validación del nombre
        var nombre = (r.Nombre ?? "").Trim();
        if (nombre.Length == 0)
            throw new NegocioException("El nombre del curso es obligatorio.");
        if (nombre.Length > 150)
            throw new NegocioException("El nombre del curso no puede superar los 150 caracteres.");

        var curso = await _db.Cursos.FirstOrDefaultAsync(c => c.IdCurso == id)
            ?? throw new NegocioException("El curso no existe.");

        // RF024: un curso cancelado (inhabilitado) no se puede editar
        if (!curso.Estado)
            throw new NegocioException("El curso está inhabilitado y no puede editarse.");

        // RF024: un curso culminado (tiene períodos y todos están finalizados) no se puede editar
        var estadosPeriodos = await _db.PeriodoInscripcions.AsNoTracking()
            .Where(p => p.IdCurso == id).Select(p => p.Estado).ToListAsync();
        if (estadosPeriodos.Count > 0 && estadosPeriodos.All(e => e == "Finalizado"))
            throw new NegocioException("El curso ya culminó (todos sus períodos están finalizados) y no puede editarse.");

        // No repetir el nombre de otro curso (sin distinguir mayúsculas)
        var repetido = await _db.Cursos.AnyAsync(c => c.IdCurso != id && c.Nombre == nombre);
        if (repetido)
            throw new NegocioException("Ya existe otro curso con ese nombre.");

        // El nivel, si se indica, debe existir y estar habilitado
        string? nombreNivel = null;
        if (r.IdNivel is not null)
        {
            var nivel = await _db.Nivels.AsNoTracking().FirstOrDefaultAsync(n => n.IdNivel == r.IdNivel)
                ?? throw new NegocioException("El nivel indicado no existe.");
            if (!nivel.Estado)
                throw new NegocioException("El nivel indicado está inhabilitado.");
            nombreNivel = nivel.Nombre;
        }

        // Para la auditoría: valores anteriores
        var anterior = $"\"{curso.Nombre}\" (nivel {curso.IdNivel?.ToString() ?? "ninguno"})";

        // Se actualiza el mismo registro (RN009)
        curso.Nombre = nombre;
        curso.IdNivel = r.IdNivel;
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(IdUsuarioActual(), "EDITAR_CURSO", "curso", id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"Curso editado: antes {anterior}; ahora \"{nombre}\" (nivel {r.IdNivel?.ToString() ?? "ninguno"}).");

        return Ok(new
        {
            curso.IdCurso,
            curso.Nombre,
            curso.IdNivel,
            Nivel = nombreNivel,
            Habilitado = curso.Estado
        });
    }
}