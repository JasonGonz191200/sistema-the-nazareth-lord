using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using sistemaTNL.DTOs;
using sistemaTNL.Interfaces;

namespace sistemaTNL.Controllers
{
    [ApiController]
    [Route("api/auth")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _auth;
        public AuthController(IAuthService auth) => _auth = auth;

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login(LoginRequest req)
        {
            var r = await _auth.LoginAsync(req, HttpContext.Connection.RemoteIpAddress?.ToString());
            if (!r.Ok) return Unauthorized(new { mensaje = r.Mensaje });

            var u = r.Usuario!;
            var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, u.IdUsuario.ToString()),
            new(ClaimTypes.Name, u.NombreUsuario),
            new(ClaimTypes.Role, u.Rol)
        };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity));

            return Ok(new { u.IdUsuario, u.NombreUsuario, u.Rol });
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return NoContent();
        }

        [HttpPost("cambiar-contrasena")]
        [Authorize]
        public async Task<IActionResult> CambiarContrasena(CambiarContrasenaRequest req)
        {
            var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var r = await _auth.CambiarContrasenaAsync(id, req, HttpContext.Connection.RemoteIpAddress?.ToString());
            return r.Ok ? Ok(new { mensaje = r.Mensaje }) : BadRequest(new { mensaje = r.Mensaje });
        }

        [HttpGet("yo")]
        [Authorize]
        public IActionResult Yo() => Ok(new
        {
            IdUsuario = User.FindFirstValue(ClaimTypes.NameIdentifier),
            NombreUsuario = User.Identity?.Name,
            Rol = User.FindFirstValue(ClaimTypes.Role)
        });
    }
}
