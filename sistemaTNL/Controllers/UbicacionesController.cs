using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.DTOs;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

[ApiController]
[Route("api/ubicaciones")]
public class UbicacionesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public UbicacionesController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    [HttpGet("departamentos")]
    public async Task<IActionResult> Departamentos() =>
        Ok(await _db.Departamentos.AsNoTracking().OrderBy(d => d.Nombre)
            .Select(d => new { d.IdDepartamento, d.Nombre }).ToListAsync());

    [HttpGet("municipios")]
    public async Task<IActionResult> Municipios([FromQuery] int idDepartamento) =>
        Ok(await _db.Municipios.AsNoTracking().Where(m => m.IdDepartamento == idDepartamento)
            .OrderBy(m => m.Nombre).Select(m => new { m.IdMunicipio, m.Nombre }).ToListAsync());

    [HttpGet("barrios")]
    public async Task<IActionResult> Barrios([FromQuery] int idMunicipio) =>
        Ok(await _db.Barrios.AsNoTracking().Where(b => b.IdMunicipio == idMunicipio)
            .OrderBy(b => b.Nombre).Select(b => new { b.IdBarrio, b.Nombre }).ToListAsync());

    // RN016: agregar un barrio que no exista, asociado a un municipio existente
    [HttpPost("barrios")]
    public async Task<IActionResult> CrearBarrio(NuevoBarrioRequest req)
    {
        var nombre = req.Nombre?.Trim();
        if (string.IsNullOrEmpty(nombre))
            throw new NegocioException("El nombre del barrio es obligatorio.");
        if (!await _db.Municipios.AnyAsync(m => m.IdMunicipio == req.IdMunicipio))
            throw new NegocioException("El municipio seleccionado no existe.");
        if (await _db.Barrios.AnyAsync(b => b.IdMunicipio == req.IdMunicipio && b.Nombre == nombre))
            throw new NegocioException("Ya existe un barrio con ese nombre en el municipio.");

        var barrio = new Barrio { Nombre = nombre, IdMunicipio = req.IdMunicipio };
        _db.Barrios.Add(barrio);
        await _db.SaveChangesAsync();

        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auditoria.RegistrarAsync(idUsuario, "CREAR_BARRIO", "barrio", barrio.IdBarrio,
            HttpContext.Connection.RemoteIpAddress?.ToString(), nombre);

        return Created($"/api/ubicaciones/barrios?idMunicipio={barrio.IdMunicipio}",
            new { barrio.IdBarrio, barrio.Nombre });
    }
    // Agregar un municipio que no exista, asociado a un departamento existente
    [HttpPost("municipios")]
    public async Task<IActionResult> CrearMunicipio(NuevoMunicipioRequest req)
    {
        // El nombre es obligatorio
        var nombre = req.Nombre?.Trim();
        if (string.IsNullOrEmpty(nombre))
            throw new NegocioException("El nombre del municipio es obligatorio.");

        // El departamento debe existir
        if (!await _db.Departamentos.AnyAsync(d => d.IdDepartamento == req.IdDepartamento))
            throw new NegocioException("El departamento seleccionado no existe.");

        // No se permite repetir el nombre dentro del mismo departamento
        if (await _db.Municipios.AnyAsync(m => m.IdDepartamento == req.IdDepartamento && m.Nombre == nombre))
            throw new NegocioException("Ya existe un municipio con ese nombre en el departamento.");

        var municipio = new Municipio { Nombre = nombre, IdDepartamento = req.IdDepartamento };
        _db.Municipios.Add(municipio);
        await _db.SaveChangesAsync();

        // Se deja constancia en la auditoría
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auditoria.RegistrarAsync(idUsuario, "CREAR_MUNICIPIO", "municipio", municipio.IdMunicipio,
            HttpContext.Connection.RemoteIpAddress?.ToString(), nombre);

        return Created($"/api/ubicaciones/municipios?idDepartamento={municipio.IdDepartamento}",
            new { municipio.IdMunicipio, municipio.Nombre });
    }
}