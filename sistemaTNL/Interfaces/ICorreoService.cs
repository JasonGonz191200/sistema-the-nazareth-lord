namespace sistemaTNL.Interfaces;

// Contrato para enviar correos desde el sistema
public interface ICorreoService
{
    // Envía un correo de texto simple al destinatario indicado
    Task EnviarAsync(string destino, string asunto, string cuerpoTexto);
}