using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Datos que llegan al crear una cuenta
public record CrearUsuarioRequest(
    int IdPersona,          // persona que ya existe en el sistema
    string NombreUsuario,   // nombre con el que va a iniciar sesión
    string Correo,
    string Rol,             // "Director" o "Subdirector"
    string Contrasena);     // contraseña inicial (mínimo 8 caracteres)

// Datos para habilitar / inhabilitar una cuenta
public record CambiarEstadoUsuarioRequest(bool Habilitado);

// Gestión de cuentas de acceso (Director / Subdirector)
[ApiController]
[Route("api/usuarios")]
public class UsuariosController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<Usuario> _hasher;
    private readonly IAuditoriaService _auditoria;

    public UsuariosController(ApplicationDbContext db, IPasswordHasher<Usuario> hasher, IAuditoriaService auditoria)
    {
        _db = db;
        _hasher = hasher;
        _auditoria = auditoria;
    }

    // Id del usuario que está haciendo la petición (viene en la cookie de sesión)
    private int IdUsuarioActual() =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : 0;

    // Mapeo común: NUNCA se devuelve el hash de la contraseña
    private static object Mapear(Usuario u) => new
    {
        u.IdUsuario,
        u.IdPersona,
        NombreCompleto = u.IdPersonaNavigation.NombreCompleto,
        u.NombreUsuario,
        u.Correo,
        u.Rol,
        Habilitado = u.Estado,
        Bloqueado = u.BloqueadoHasta != null && u.BloqueadoHasta > DateTime.Now,
        u.BloqueadoHasta
    };

    // GET api/usuarios  → lista todas las cuentas
    [HttpGet]
    public async Task<IActionResult> Listar() =>
        Ok(await _db.Usuarios.AsNoTracking()
            .Include(u => u.IdPersonaNavigation)
            .OrderBy(u => u.NombreUsuario)
            .Select(u => Mapear(u))
            .ToListAsync());

    // POST api/usuarios  → crea una cuenta para una persona existente
    [HttpPost]
    public async Task<IActionResult> Crear(CrearUsuarioRequest r)
    {
        // Validaciones de formato
        var nombre = (r.NombreUsuario ?? "").Trim();
        var correo = (r.Correo ?? "").Trim();
        if (nombre.Length < 3)
            throw new NegocioException("El nombre de usuario debe tener al menos 3 caracteres.");
        if (!correo.Contains('@') || correo.Length < 5)
            throw new NegocioException("El correo no tiene un formato válido.");
        if (r.Rol != "Director" && r.Rol != "Subdirector")
            throw new NegocioException("El rol debe ser \"Director\" o \"Subdirector\".");
        if (string.IsNullOrEmpty(r.Contrasena) || r.Contrasena.Length < 8)
            throw new NegocioException("La contraseña debe tener al menos 8 caracteres.");

        // La persona debe existir y estar activa
        var persona = await _db.Personas.FirstOrDefaultAsync(p => p.IdPersona == r.IdPersona)
            ?? throw new NegocioException("La persona indicada no existe.");
        if (!persona.Estado)
            throw new NegocioException("La persona está inhabilitada.");

        // Una persona solo puede tener una cuenta
        if (await _db.Usuarios.AnyAsync(u => u.IdPersona == r.IdPersona))
            throw new NegocioException("Esa persona ya tiene una cuenta de usuario.");

        // Nombre de usuario y correo únicos
        if (await _db.Usuarios.AnyAsync(u => u.NombreUsuario == nombre))
            throw new NegocioException("Ya existe un usuario con ese nombre.");
        if (await _db.Usuarios.AnyAsync(u => u.Correo == correo))
            throw new NegocioException("Ya existe un usuario con ese correo.");

        var usuario = new Usuario
        {
            IdPersona = r.IdPersona,
            NombreUsuario = nombre,
            Correo = correo,
            Rol = r.Rol,
            Estado = true,
            IntentosFallidos = 0
        };
        // La contraseña se guarda siempre como hash
        usuario.ContrasenaHash = _hasher.HashPassword(usuario, r.Contrasena);

        _db.Usuarios.Add(usuario);
        await _db.SaveChangesAsync();

        // Auditoría (sin incluir la contraseña)
        await _auditoria.RegistrarAsync(IdUsuarioActual(), "CREAR_USUARIO", "usuario", usuario.IdUsuario,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"Cuenta \"{nombre}\" creada con rol {r.Rol}.");

        usuario.IdPersonaNavigation = persona;
        return Ok(Mapear(usuario));
    }

    // PUT api/usuarios/{id}/estado  → habilita o inhabilita una cuenta
    [HttpPut("{id:int}/estado")]
    public async Task<IActionResult> CambiarEstado(int id, CambiarEstadoUsuarioRequest r)
    {
        var usuario = await _db.Usuarios.Include(u => u.IdPersonaNavigation)
            .FirstOrDefaultAsync(u => u.IdUsuario == id)
            ?? throw new NegocioException("El usuario no existe.");

        // Nadie puede inhabilitar su propia cuenta (evita quedarse sin acceso)
        if (!r.Habilitado && id == IdUsuarioActual())
            throw new NegocioException("No podés inhabilitar tu propia cuenta.");

        if (usuario.Estado == r.Habilitado)
            throw new NegocioException(r.Habilitado
                ? "La cuenta ya está habilitada."
                : "La cuenta ya está inhabilitada.");

        usuario.Estado = r.Habilitado;
        // Al habilitar, se limpian bloqueos e intentos fallidos
        if (r.Habilitado)
        {
            usuario.IntentosFallidos = 0;
            usuario.BloqueadoHasta = null;
        }
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(IdUsuarioActual(),
            r.Habilitado ? "HABILITAR_USUARIO" : "INHABILITAR_USUARIO", "usuario", id,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            $"Cuenta \"{usuario.NombreUsuario}\" {(r.Habilitado ? "habilitada" : "inhabilitada")}.");

        return Ok(Mapear(usuario));
    }
}