using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.DTOs;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Data;


namespace sistemaTNL.Services
{
    public class AuthService : IAuthService
    {
        private const string MensajeGenerico = "usuario o contraseña incorrectos";
        private readonly ApplicationDbContext _dbContext;
        private readonly IPasswordHasher<Usuario> _hasher;

        public AuthService(ApplicationDbContext dbContext, IPasswordHasher<Usuario> hasher)
        {
            _dbContext = dbContext;
            _hasher = hasher;
        }
        public async Task<AuthResult> LoginAsync(LoginRequest request, string? ip)
        {
            if (string.IsNullOrWhiteSpace(request.NombreUsuario) || string.IsNullOrWhiteSpace(request.Contrasena))
                return new(false, "complete la contraseña y el usuario");


            var maxIntentos = await ParametroAsync("MAX_INTENTOS_LOGIN", 5);
            var minutisBloqueo = await ParametroAsync("MINUTOS_BLOQUEO", 15);
            var hora = DateTime.UtcNow;

            var u = await _dbContext.Usuarios.FirstOrDefaultAsync(x => x.NombreUsuario == request.NombreUsuario);
            if (u == null)
            {
                await AuditarAsync(null, request.NombreUsuario, "LOGIN", null, "FALLIDO", ip, "USUARIO INEXISTENTE");
                return new(false, MensajeGenerico);
            }

            if (u.BloqueadoHasta.HasValue && u.BloqueadoHasta.Value > hora)
            {
                var faltan = (int)Math.Ceiling((u.BloqueadoHasta.Value - hora).TotalMinutes);
                await AuditarAsync(u.IdUsuario, u.NombreUsuario, "LOGIN", u.IdUsuario, "RECHAZADO", ip, "CUENTA BLOQUEADA");
                return new(false, $"Cuenta bloqueada temporalmente. Intente de nuevo en {faltan} minuto(s).");
            }
            var verif = _hasher.VerifyHashedPassword(u, u.ContrasenaHash, request.Contrasena);
            if (verif == PasswordVerificationResult.Failed)
            {
                u.IntentosFallidos++;
                var bloquear = u.IntentosFallidos >= maxIntentos;
                if (bloquear)
                {
                    u.BloqueadoHasta = hora.AddMinutes(minutisBloqueo);
                    u.IntentosFallidos = 0;
                }
                await _dbContext.SaveChangesAsync();
                await AuditarAsync(u.IdUsuario, u.NombreUsuario, bloquear? "Bloqueo de cuenta" : "LOGIN", u.IdUsuario, "FALLIDO", ip, bloquear ? "CUENTA BLOQUEADA" : "CONTRASEÑA INCORRECTA");
                return new(false, MensajeGenerico);
            }
            if(!u.Estado)
            {
                await AuditarAsync(u.IdUsuario, u.NombreUsuario, "LOGIN", u.IdUsuario, "RECHAZADO", ip, "CUENTA INHABILITADA");
                return new(false, "La cuenta está inhabilitada");
            }
            if(verif == PasswordVerificationResult.SuccessRehashNeeded)
            {
                u.ContrasenaHash = _hasher.HashPassword(u, request.Contrasena);
            }
            try
            {
                verif = _hasher.VerifyHashedPassword(u, u.ContrasenaHash, request.Contrasena);
            }
            catch (FormatException)
            {
                verif = PasswordVerificationResult.Failed; // hash guardado con formato inválido
            }

            u.IntentosFallidos = 0;
            u.BloqueadoHasta = null;
            await _dbContext.SaveChangesAsync();
            await AuditarAsync(u.IdUsuario, u.NombreUsuario, "LOGIN", u.IdUsuario, "EXITOSO", ip, null);
            return new(true, null, u);


        }
        public async Task<AuthResult> CambiarContrasenaAsync(int idUsuario, CambiarContrasenaRequest request, string? ip)
        {
           var u = await _dbContext.Usuarios.FindAsync(idUsuario);
            if (u is null || !u.Estado)
                return new(false, "Sesión no válida");
            if(string.IsNullOrWhiteSpace(request.ContrasenaActual) || string.IsNullOrWhiteSpace(request.NuevaContrasena))
                return new(false, "Complete todos los campos");
            if (_hasher.VerifyHashedPassword(u, u.ContrasenaHash, request.ContrasenaActual) == PasswordVerificationResult.Failed)
            {
                await AuditarAsync(u.IdUsuario, u.NombreUsuario, "CAMBIO_CONTRASENA", u.IdUsuario, "Fallido", ip, "Contraseña actual incorrecta");
                return new(false, "La contraseña actual es incorrecta.");
            }
            if (request.NuevaContrasena.Length < 8)
                return new(false, "La nueva contraseña debe tener al menos 8 caracteres.");
            if (request.NuevaContrasena != request.Confirmacion)
                return new(false, "La confirmación no coincide con la nueva contraseña.");
            if (request.NuevaContrasena == request.ContrasenaActual)
                return new(false, "La nueva contraseña debe ser distinta de la actual.");

            u.ContrasenaHash = _hasher.HashPassword(u, request.NuevaContrasena);
            await _dbContext.SaveChangesAsync();
            await AuditarAsync(u.IdUsuario, u.NombreUsuario, "CAMBIO_CONTRASENA", u.IdUsuario, "Exitoso", ip, null);
            return new(true, "Contraseña actualizada correctamente.");
        }
        private async Task<int> ParametroAsync(string clave, int porDefecto)
        {
            var valor = await _dbContext.Parametros.Where(p => p.Clave == clave).Select(p => p.Valor).FirstOrDefaultAsync();
            return int.TryParse(valor, out var n) ? n : porDefecto;
        }
        private async Task AuditarAsync(int? idUsuario, string? intento, string accion, int? idRegistro, string resultado, string? ip, string? descripcion)
        {
            _dbContext.RegistroAuditoria.Add(new RegistroAuditorium
            {
                IdUsuario = idUsuario,
                NombreUsuarioIntento = idUsuario is null ? intento : null,
                FechaHora = DateTimeOffset.Now,
                TipoAccion = accion,
                EntidadAfectada = "usuario",
                IdRegistroAfectado = idRegistro,
                Resultado = resultado,
                DireccionIp = ip,
                Descripcion = descripcion
            });
            await _dbContext.SaveChangesAsync();
        }
    }
    
    
}
