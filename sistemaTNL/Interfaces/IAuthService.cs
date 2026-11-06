using sistemaTNL.DTOs;
using sistemaTNL.Entities;

namespace sistemaTNL.Interfaces
{
    public interface IAuthService
    {
        Task<AuthResult> LoginAsync(LoginRequest request, string? ip);
        Task<AuthResult> CambiarContrasenaAsync(int idUsuario, CambiarContrasenaRequest request, string? ip);
    }
}