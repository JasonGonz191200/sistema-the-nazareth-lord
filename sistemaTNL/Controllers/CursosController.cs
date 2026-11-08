using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Cuerpo para crear un curso. El nivel es opcional (en la tabla curso es nullable).
public record CrearCursoRequest(string Nombre, int? IdNivel);

// Cuerpo para habilitar (true) o inhabilitar (false) un curso
public record CambiarEstadoCursoRequest(bool Habilitado);

[ApiController]
[Route("api/cursos")]
public class CursosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public CursosController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // GET api/cursos/niveles
    // Lista los niveles para elegir uno al crear un curso
    [HttpGet("niveles")]
    public async Task<IActionResult> Niveles() =>
        Ok(await _db.Nivels
            .AsNoTracking()
            .Where(n => n.Estado)                  // solo niveles habilitados
            .OrderBy(n => n.Orden).ThenBy(n => n.Nombre)
            .Select(n => new { n.IdNivel, n.Nombre, n.Orden })
            .ToListAsync());

    // GET api/cursos
    // Lista todos los cursos con el nombre de su nivel
    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await _db.Cursos
            .AsNoTracking()
            .OrderBy(c => c.Nombre)
            .Select(c => new
            {
                c.IdCurso,
                c.Nombre,
                c.IdNivel,
                Nivel = c.IdNivelNavigation != null ? c.IdNivelNavigation.Nombre : null,
                c.Estado
            })
            .ToListAsync());

    // POST api/cursos
    // Ejemplo: { "nombre": "Inglés General", "idNivel": 1 }
    [HttpPost]
    public async Task<IActionResult> Crear(CrearCursoRequest req)
    {
        // Validaciones que conviene decir de forma amable
        var nombre = req.Nombre?.Trim();
        if (string.IsNullOrEmpty(nombre))
            throw new NegocioException("El nombre del curso es obligatorio.");

        if (req.IdNivel is not null &&
            !await _db.Nivels.AnyAsync(n => n.IdNivel == req.IdNivel && n.Estado))
            throw new NegocioException("El nivel seleccionado no existe o está inhabilitado.");

        // La tabla no tiene restricción de nombre único, así que lo validamos aquí
        if (await _db.Cursos.AnyAsync(c => c.Nombre == nombre))
            throw new NegocioException("Ya existe un curso con ese nombre.");

        var curso = new Curso { Nombre = nombre, IdNivel = req.IdNivel, Estado = true };
        _db.Cursos.Add(curso);
        await _db.SaveChangesAsync();

        // Auditoría: quién creó el curso
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auditoria.RegistrarAsync(idUsuario, "CREAR_CURSO", "curso", curso.IdCurso,
            HttpContext.Connection.RemoteIpAddress?.ToString(), nombre);

        return Created($"/api/cursos", new { curso.IdCurso, curso.Nombre, curso.IdNivel, curso.Estado });
    }

    // PUT api/cursos/{id}/estado
    // Ejemplo: PUT api/cursos/1/estado con { "habilitado": false }
    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoCursoRequest req)
    {
        var curso = await _db.Cursos.FirstOrDefaultAsync(c => c.IdCurso == id)
            ?? throw new NegocioException("El curso no existe.");

        // Evitamos "cambiar" a un estado en el que ya está
        if (curso.Estado == req.Habilitado)
            throw new NegocioException(
                $"El curso ya está {(req.Habilitado ? "habilitado" : "inhabilitado")}.");

        curso.Estado = req.Habilitado;

        // El trigger puede rechazar el cambio si el curso tiene períodos habilitados (RF025);
        // el middleware lo convierte en un 400 con ese mensaje.
        await _db.SaveChangesAsync();

        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auditoria.RegistrarAsync(idUsuario,
            req.Habilitado ? "HABILITAR_CURSO" : "INHABILITAR_CURSO",
            "curso", curso.IdCurso,
            HttpContext.Connection.RemoteIpAddress?.ToString(), curso.Nombre);

        return Ok(new { curso.IdCurso, curso.Nombre, curso.Estado });
    }
}