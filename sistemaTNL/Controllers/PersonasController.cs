using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using sistemaTNL.DTOs;
using sistemaTNL.Interfaces;

namespace sistemaTNL.Controllers;

[ApiController]
[Route("api/personas")]
public class PersonasController : ControllerBase
{
    private readonly IPersonaService _personas;
    public PersonasController(IPersonaService personas) => _personas = personas;

    [HttpGet("buscar")]
    public async Task<IActionResult> Buscar([FromQuery] string cedula)
    {
        var p = await _personas.BuscarPorCedulaAsync(cedula);
        return p is null
            ? NotFound(new { mensaje = "No existe una persona registrada con esa cédula." })
            : Ok(p);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var p = await _personas.ObtenerAsync(id);
        return p is null ? NotFound(new { mensaje = "La persona no existe." }) : Ok(p);
    }

    [HttpGet("tutores")]
    public async Task<IActionResult> Tutores([FromQuery] string? buscar) =>
        Ok(await _personas.ListarTutoresAsync(buscar));

    [HttpPost]
    public async Task<IActionResult> Registrar(RegistrarPersonaRequest req)
    {
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var r = await _personas.RegistrarAsync(req, idUsuario,
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return Created($"/api/personas/{r.IdPersona}", r);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, ActualizarPersonaRequest req)
    {
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var r = await _personas.ActualizarAsync(id, req, idUsuario,
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(r);
    }
    // PUT api/personas/{id}/roles/{rol}/estado
    // Ejemplo: PUT api/personas/2/roles/tutor/estado con { "habilitado": false }
    [HttpPut("{id:int}/roles/{rol}/estado")]
    public async Task<IActionResult> CambiarEstadoRol(int id, string rol, CambiarEstadoRolRequest req)
    {
        // Id del usuario que inició sesión, para la auditoría
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        var r = await _personas.CambiarEstadoRolAsync(id, rol, req.Habilitado, idUsuario,
            HttpContext.Connection.RemoteIpAddress?.ToString());
        return Ok(r);
    }
}