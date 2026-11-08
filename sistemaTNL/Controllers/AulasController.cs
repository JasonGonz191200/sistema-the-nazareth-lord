using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Cuerpo para crear un aula
public record CrearAulaRequest(string Nombre);

// Cuerpo para habilitar (true) o inhabilitar (false) un aula
public record CambiarEstadoAulaRequest(bool Habilitado);

[ApiController]
[Route("api/aulas")]
public class AulasController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public AulasController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // GET api/aulas
    // Lista todas las aulas, habilitadas o no
    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await _db.Aulas
            .AsNoTracking()                        // solo lectura
            .OrderBy(a => a.Nombre)
            .Select(a => new { a.IdAula, a.Nombre, a.Estado })
            .ToListAsync());

    // POST api/aulas
    // Ejemplo: { "nombre": "Aula 1" }
    [HttpPost]
    public async Task<IActionResult> Crear(CrearAulaRequest req)
    {
        var nombre = req.Nombre?.Trim();
        if (string.IsNullOrEmpty(nombre))
            throw new NegocioException("El nombre del aula es obligatorio.");

        // La base ya impide nombres repetidos (UQ_aula_nombre); lo validamos antes
        // para devolver un mensaje claro en vez de un error genérico
        if (await _db.Aulas.AnyAsync(a => a.Nombre == nombre))
            throw new NegocioException("Ya existe un aula con ese nombre.");

        var aula = new Aula { Nombre = nombre, Estado = true };
        _db.Aulas.Add(aula);
        await _db.SaveChangesAsync();

        // Auditoría: quién creó el aula
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auditoria.RegistrarAsync(idUsuario, "CREAR_AULA", "aula", aula.IdAula,
            HttpContext.Connection.RemoteIpAddress?.ToString(), nombre);

        return Created("/api/aulas", new { aula.IdAula, aula.Nombre, aula.Estado });
    }

    // PUT api/aulas/{id}/estado
    // Ejemplo: PUT api/aulas/1/estado con { "habilitado": false }
    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoAulaRequest req)
    {
        var aula = await _db.Aulas.FirstOrDefaultAsync(a => a.IdAula == id)
            ?? throw new NegocioException("El aula no existe.");

        // Evitamos "cambiar" a un estado en el que ya está
        if (aula.Estado == req.Habilitado)
            throw new NegocioException(
                $"El aula ya está {(req.Habilitado ? "habilitada" : "inhabilitada")}.");

        aula.Estado = req.Habilitado;
        await _db.SaveChangesAsync();

        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auditoria.RegistrarAsync(idUsuario,
            req.Habilitado ? "HABILITAR_AULA" : "INHABILITAR_AULA",
            "aula", aula.IdAula,
            HttpContext.Connection.RemoteIpAddress?.ToString(), aula.Nombre);

        return Ok(new { aula.IdAula, aula.Nombre, aula.Estado });
    }
}