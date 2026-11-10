using MailKit;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using sistemaTNL.Interfaces;

namespace sistemaTNL.Services;

// Envío de correos por SMTP (Gmail). Los datos salen de la sección "Correo"
// de los secretos de usuario: Servidor, Puerto, Usuario, Clave, Remitente.
public class CorreoService : ICorreoService
{
    private readonly IConfiguration _config;

    public CorreoService(IConfiguration config) => _config = config;

    public async Task EnviarAsync(string destino, string asunto, string cuerpoTexto)
    {
        var servidor = _config["Correo:Servidor"];
        var usuario = _config["Correo:Usuario"];
        var clave = _config["Correo:Clave"];
        var remitente = _config["Correo:Remitente"] ?? usuario;
        var puerto = int.TryParse(_config["Correo:Puerto"], out var p) ? p : 587;

        // Si falta algo, se avisa con un error claro en lugar de fallar en silencio
        if (string.IsNullOrWhiteSpace(servidor) || string.IsNullOrWhiteSpace(usuario) ||
            string.IsNullOrWhiteSpace(clave) || string.IsNullOrWhiteSpace(remitente))
            throw new InvalidOperationException("Falta la configuración de correo (sección \"Correo\" en los secretos de usuario).");

        // Armado del mensaje
        var mensaje = new MimeMessage();
        mensaje.From.Add(new MailboxAddress("Academia The Nazareth Lord", remitente));
        mensaje.To.Add(MailboxAddress.Parse(destino));
        mensaje.Subject = asunto;
        mensaje.Body = new TextPart("plain") { Text = cuerpoTexto };

        // Conexión segura (STARTTLS en el puerto 587), autenticación y envío
        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(servidor, puerto, SecureSocketOptions.StartTls);
        await smtp.AuthenticateAsync(usuario, clave);
        await smtp.SendAsync(mensaje);
        await smtp.DisconnectAsync(true);
    }
}