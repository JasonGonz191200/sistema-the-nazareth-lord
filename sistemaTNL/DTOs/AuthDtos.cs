using sistemaTNL.Entities;
namespace sistemaTNL.DTOs
{
    public record LoginRequest(string NombreUsuario, string Contrasena);
    public record CambiarContrasenaRequest(string ContrasenaActual, string NuevaContrasena, string Confirmacion);
    public record AuthResult(bool Ok, string? Mensaje, Entities.Usuario? Usuario = null);
}
