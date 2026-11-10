using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Consulta del registro de auditoría (RF037). Solo lectura.
[ApiController]
[Route("api/auditoria")]
public class AuditoriaController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public AuditoriaController(ApplicationDbContext db) => _db = db;

    // GET api/auditoria/tipos-accion
    // Lista los tipos de acción que existen en el registro, para elegirlos en el filtro
    [HttpGet("tipos-accion")]
    public async Task<IActionResult> TiposAccion() =>
        Ok(await _db.RegistroAuditoria.AsNoTracking()
            .Select(r => r.TipoAccion)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync());

    // GET api/auditoria?usuario=admin&desde=2026-11-01&hasta=2026-11-30&tipoAccion=REGISTRAR_PAGO&resultado=Exitoso&pagina=1&tamano=50
    // Todos los filtros son opcionales y se pueden combinar
    [HttpGet]
    public async Task<IActionResult> Consultar(
        [FromQuery] string? usuario,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] string? tipoAccion,
        [FromQuery] string? resultado,
        [FromQuery] int pagina = 1,
        [FromQuery] int tamano = 50)
    {
        if (pagina < 1)
            throw new NegocioException("La página debe ser 1 o mayor.");
        if (tamano < 1 || tamano > 200)
            throw new NegocioException("El tamaño de página debe estar entre 1 y 200.");
        if (desde is not null && hasta is not null && desde > hasta)
            throw new NegocioException("La fecha \"desde\" no puede ser posterior a la fecha \"hasta\".");

        var q = _db.RegistroAuditoria.AsNoTracking().AsQueryable();

        // Usuario: busca por nombre de usuario, incluso en intentos con un usuario que no existe
        if (!string.IsNullOrWhiteSpace(usuario))
        {
            var u = usuario.Trim();
            q = q.Where(r =>
                (r.IdUsuarioNavigation != null && r.IdUsuarioNavigation.NombreUsuario.Contains(u)) ||
                (r.NombreUsuarioIntento != null && r.NombreUsuarioIntento.Contains(u)));
        }

        // Fechas: "desde" incluye todo el día; "hasta" también (se compara contra el día siguiente)
        if (desde is not null)
        {
            var d = new DateTimeOffset(desde.Value.ToDateTime(TimeOnly.MinValue));
            q = q.Where(r => r.FechaHora >= d);
        }
        if (hasta is not null)
        {
            var h = new DateTimeOffset(hasta.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
            q = q.Where(r => r.FechaHora < h);
        }

        if (!string.IsNullOrWhiteSpace(tipoAccion))
        {
            var t = tipoAccion.Trim();
            q = q.Where(r => r.TipoAccion == t);
        }

        if (!string.IsNullOrWhiteSpace(resultado))
        {
            var res = resultado.Trim();
            q = q.Where(r => r.Resultado == res);
        }

        var total = await q.CountAsync();

        // Lo más reciente primero
        var registros = await q
            .OrderByDescending(r => r.IdAuditoria)
            .Skip((pagina - 1) * tamano)
            .Take(tamano)
            .Select(r => new
            {
                r.IdAuditoria,
                r.FechaHora,
                Usuario = r.IdUsuarioNavigation != null
                    ? r.IdUsuarioNavigation.NombreUsuario
                    : r.NombreUsuarioIntento,
                r.TipoAccion,
                Entidad = r.EntidadAfectada,
                RegistroAfectado = r.IdRegistroAfectado,
                r.Resultado,
                Ip = r.DireccionIp,
                r.Descripcion
            })
            .ToListAsync();

        return Ok(new
        {
            total,
            pagina,
            tamano,
            registros,
            mensaje = total == 0 ? "No se encontraron registros que coincidan con los criterios de búsqueda." : null
        });
    }
}