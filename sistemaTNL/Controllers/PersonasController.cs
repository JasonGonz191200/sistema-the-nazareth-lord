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
}