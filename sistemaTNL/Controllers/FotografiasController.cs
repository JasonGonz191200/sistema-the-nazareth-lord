using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Fotografía de una persona. Requiere sesión iniciada (política global de la aplicación).
[ApiController]
[Route("api/personas/{idPersona:int}/fotografia")]
public class FotografiasController : ControllerBase
{
    // Tamaño máximo permitido de la imagen: 2 MB
    private const int MaxBytes = 2 * 1024 * 1024;

    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public FotografiasController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // Id del usuario que hace la petición (cookie de sesión)
    private int IdUsuarioActual() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Detecta el tipo de imagen por los primeros bytes del archivo (no por la extensión)
    private static string? DetectarTipo(byte[] b)
    {
        // JPEG empieza con FF D8 FF
        if (b.Length > 3 && b[0] == 0xFF && b[1] == 0xD8 && b[2] == 0xFF) return "image/jpeg";
        // PNG empieza con 89 50 4E 47 0D 0A 1A 0A
        if (b.Length > 8 && b[0] == 0x89 && b[1] == 0x50 && b[2] == 0x4E && b[3] == 0x47 &&
            b[4] == 0x0D && b[5] == 0x0A && b[6] == 0x1A && b[7] == 0x0A) return "image/png";
        return null;
    }

    // GET api/personas/{idPersona}/fotografia  → devuelve la imagen
    [HttpGet]
    public async Task<IActionResult> Obtener(int idPersona)
    {
        var foto = await _db.Personas.AsNoTracking()
            .Where(p => p.IdPersona == idPersona)
            .Select(p => new { Existe = true, p.Fotografia })
            .FirstOrDefaultAsync()
            ?? throw new NegocioException("La persona no existe.");

        if (foto.Fotografia is null || foto.Fotografia.Length == 0)
            throw new NegocioException("La persona no tiene fotografía registrada.");

        var tipo = DetectarTipo(foto.Fotografia) ?? "application/octet-stream";

        // La imagen contiene datos personales: que ningún intermediario la guarde en caché
        Response.Headers.CacheControl = "private, no-store";
        return File(foto.Fotografia, tipo);
    }

    // PUT api/personas/{idPersona}/fotografia   (form-data, campo "archivo")
    [HttpPut]
    [RequestSizeLimit(3 * 1024 * 1024)]   // corta peticiones demasiado grandes antes de leerlas
    public async Task<IActionResult> Subir(int idPersona, IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
            throw new NegocioException("Debe seleccionar una imagen.");
        if (archivo.Length > MaxBytes)
            throw new NegocioException("La imagen no puede superar los 2 MB.");

        // Se lee el archivo completo en memoria (máximo 2 MB)
        using var ms = new MemoryStream();
        await archivo.CopyToAsync(ms);
        var bytes = ms.ToArray();

        // Solo JPG o PNG, comprobando el contenido real
        if (DetectarTipo(bytes) is null)
            throw new NegocioException("El archivo debe ser una imagen JPG o PNG válida.");

        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.IdPersona == idPersona)
            ?? throw new NegocioException("La persona no existe.");

        var teniaFoto = persona.Fotografia is { Length: > 0 };
        persona.Fotografia = bytes;
        await _db.SaveChangesAsync();

        // En la auditoría NO se guarda la imagen, solo el hecho del cambio
        await _auditoria.RegistrarAsync(IdUsuarioActual(), "ACTUALIZAR_FOTOGRAFIA", "persona", idPersona,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            teniaFoto ? "Fotografía reemplazada." : "Fotografía registrada.");

        return Ok(new { idPersona, tamanoBytes = bytes.Length, mensaje = "La fotografía se guardó correctamente." });
    }
}