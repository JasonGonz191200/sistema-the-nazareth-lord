using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Cuerpo de la petición: true = habilitar la modalidad, false = inhabilitarla
public record CambiarEstadoModalidadRequest(bool Habilitado);

[ApiController]
[Route("api/modalidades")]
public class ModalidadesController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public ModalidadesController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // GET api/modalidades
    // Lista todas las modalidades, habilitadas o no, con su horario predeterminado
    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await _db.Modalidads
            .AsNoTracking()                       // solo lectura, no necesitamos seguimiento de cambios
            .OrderBy(m => m.Nombre)
            .Select(m => new
            {
                m.IdModalidad,
                m.Nombre,
                m.HorarioPredeterminado,
                m.Estado
            })
            .ToListAsync());

    // PUT api/modalidades/{id}/estado
    // Ejemplo: PUT api/modalidades/1/estado con { "habilitado": false }
    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoModalidadRequest req)
    {
        // Buscamos la modalidad; si no existe, respondemos con un mensaje claro
        var modalidad = await _db.Modalidads.FirstOrDefaultAsync(m => m.IdModalidad == id)
            ?? throw new NegocioException("La modalidad no existe.");

        // Evitamos "cambiar" a un estado en el que ya está
        if (modalidad.Estado == req.Habilitado)
            throw new NegocioException(
                $"La modalidad ya está {(req.Habilitado ? "habilitada" : "inhabilitada")}.");

        modalidad.Estado = req.Habilitado;

        // Aquí el trigger de la base puede rechazar el cambio (RF011: la modalidad
        // tiene períodos habilitados asociados). El middleware lo convierte en un 400.
        await _db.SaveChangesAsync();

        // Id del usuario que inició sesión, para la auditoría
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auditoria.RegistrarAsync(
            idUsuario,
            req.Habilitado ? "HABILITAR_MODALIDAD" : "INHABILITAR_MODALIDAD",
            "modalidad", modalidad.IdModalidad,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            modalidad.Nombre);

        return Ok(new { modalidad.IdModalidad, modalidad.Nombre, modalidad.Estado });
    }
}