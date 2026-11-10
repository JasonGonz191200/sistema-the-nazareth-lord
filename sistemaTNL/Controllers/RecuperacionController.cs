using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Datos que llegan al solicitar la recuperación
public record SolicitarRecuperacionRequest(string Correo);

// Datos que llegan al restablecer la contraseña
public record RestablecerContrasenaRequest(string Token, string NuevaContrasena);

// Recuperación de contraseña (RNF001). Son endpoints PÚBLICOS: se usan cuando la persona no puede iniciar sesión.
[ApiController]
[AllowAnonymous]
[Route("api/auth")]
public class RecuperacionController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<Usuario> _hasher;
    private readonly IAuditoriaService _auditoria;
    private readonly ILogger<RecuperacionController> _logger;
    private readonly IWebHostEnvironment _env;
    private readonly ICorreoService _correo;

    public RecuperacionController(
        ApplicationDbContext db,
        IPasswordHasher<Usuario> hasher,
        IAuditoriaService auditoria,
        ILogger<RecuperacionController> logger,
        IWebHostEnvironment env,
        ICorreoService correo)
    {
        _db = db;
        _hasher = hasher;
        _auditoria = auditoria;
        _logger = logger;
        _env = env;
        _correo = correo;
    }

    // POST api/auth/recuperar   body: { "correo": "..." }
    [HttpPost("recuperar")]
    public async Task<IActionResult> Solicitar(SolicitarRecuperacionRequest r)
    {
        // Mensaje único: no revela si el correo existe o no
        const string respuesta =
            "Si el correo corresponde a una cuenta habilitada, se enviará un enlace para restablecer la contraseña.";

        var correo = (r.Correo ?? "").Trim();
        if (correo.Length == 0)
            throw new NegocioException("Debe indicar el correo.");

        var usuario = await _db.Usuarios.FirstOrDefaultAsync(u => u.Correo == correo && u.Estado);
        if (usuario is null)
            return Ok(new { mensaje = respuesta });

        // Vigencia del token: parámetro de la base (por defecto 30 minutos)
        var minutos = await LeerParametroAsync("MINUTOS_VIGENCIA_TOKEN", 30);

        // Los tokens anteriores todavía sin usar de esta cuenta quedan invalidados
        var pendientes = await _db.TokenRecuperacions
            .Where(t => t.IdUsuario == usuario.IdUsuario && !t.Usado).ToListAsync();
        foreach (var p in pendientes) p.Usado = true;

        // Token aleatorio de 32 bytes, en un formato seguro para direcciones web
        // Base64 adaptado a direcciones web: "+" -> "-", "/" -> "_" y sin relleno "="
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');

        var ahora = DateTime.Now;
        _db.TokenRecuperacions.Add(new TokenRecuperacion
        {
            IdUsuario = usuario.IdUsuario,
            TokenHash = HashToken(token),      // en la base solo se guarda el hash
            FechaCreacion = ahora,
            FechaExpiracion = ahora.AddMinutes(minutos),
            Usado = false
        });
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(usuario.IdUsuario, "SOLICITAR_RECUPERACION", "usuario", usuario.IdUsuario,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            "Se solicitó la recuperación de contraseña.");   // sin incluir el token

        await EnviarCorreoAsync(usuario.Correo, token, minutos);

        return Ok(new { mensaje = respuesta });
    }

    // POST api/auth/restablecer   body: { "token": "...", "nuevaContrasena": "..." }
    [HttpPost("restablecer")]
    public async Task<IActionResult> Restablecer(RestablecerContrasenaRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Token))
            throw new NegocioException("El enlace de recuperación no es válido.");
        if (string.IsNullOrEmpty(r.NuevaContrasena) || r.NuevaContrasena.Length < 8)
            throw new NegocioException("La contraseña debe tener al menos 8 caracteres.");

        var hash = HashToken(r.Token.Trim());
        var ahora = DateTime.Now;

        // El token debe existir, no estar usado y no haber vencido
        var token = await _db.TokenRecuperacions.Include(t => t.IdUsuarioNavigation)
            .FirstOrDefaultAsync(t => t.TokenHash == hash && !t.Usado && t.FechaExpiracion > ahora);

        // Mismo mensaje para token inexistente, usado o vencido
        if (token is null || !token.IdUsuarioNavigation.Estado)
            throw new NegocioException("El enlace de recuperación no es válido o ya venció. Solicite uno nuevo.");

        var usuario = token.IdUsuarioNavigation;

        // Cambia la contraseña (siempre como hash) y desbloquea la cuenta
        usuario.ContrasenaHash = _hasher.HashPassword(usuario, r.NuevaContrasena);
        usuario.IntentosFallidos = 0;
        usuario.BloqueadoHasta = null;

        // El token queda gastado: un solo uso
        token.Usado = true;
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(usuario.IdUsuario, "RESTABLECER_CONTRASENA", "usuario", usuario.IdUsuario,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            "Contraseña restablecida mediante recuperación.");

        return Ok(new { mensaje = "La contraseña se restableció correctamente. Ya puede iniciar sesión." });
    }

    // SHA-256 del token en hexadecimal (el token ya es aleatorio y largo, no necesita sal)
    private static string HashToken(string token) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    // Envío del correo. Todavía no hay servidor SMTP: en Development el enlace se muestra en la consola.
    // Cuando exista el SMTP, solo hay que reemplazar el contenido de este método.
    // Envía el correo con el token. Si el envío falla, se registra el error pero NO se
    // le dice a quien consulta (la respuesta pública debe ser siempre la misma).
    private async Task EnviarCorreoAsync(string correo, string token, int minutos)
    {
        // Solo en desarrollo: además se muestra el token en la consola para pruebas
        if (_env.IsDevelopment())
            _logger.LogWarning(
                "[SOLO DESARROLLO] Recuperación para {Correo}. Token: {Token} (vence en {Minutos} min)",
                correo, token, minutos);

        var cuerpo =
            "Recibimos una solicitud para restablecer la contraseña de su cuenta en el sistema de " +
            "la Academia de Inglés The Nazareth Lord.\n\n" +
            $"Su código de recuperación es:\n\n{token}\n\n" +
            $"El código vence en {minutos} minutos y solo se puede usar una vez.\n\n" +
            "Si usted no solicitó este cambio, ignore este mensaje: su contraseña no se modificó.";

        try
        {
            await _correo.EnviarAsync(correo, "Recuperación de contraseña - The Nazareth Lord", cuerpo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "No se pudo enviar el correo de recuperación.");
        }
    }

    // Lee un parámetro numérico de la tabla parametro
    private async Task<int> LeerParametroAsync(string clave, int porDefecto)
    {
        var valor = await _db.Parametros.AsNoTracking()
            .Where(p => p.Clave == clave).Select(p => p.Valor).FirstOrDefaultAsync();
        return int.TryParse(valor, out var n) ? n : porDefecto;
    }
}