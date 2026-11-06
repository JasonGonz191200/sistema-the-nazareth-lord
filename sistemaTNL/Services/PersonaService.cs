using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.DTOs;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;

namespace sistemaTNL.Services;

public class PersonaService : IPersonaService
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public PersonaService(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    public async Task<PersonaResumen?> BuscarPorCedulaAsync(string cedula)
    {
        var ced = NormalizarCedula(cedula);
        if (ced is null) return null;
        var p = await Consulta().FirstOrDefaultAsync(x => x.Cedula == ced);
        return p is null ? null : Mapear(p, false);
    }

    public async Task<PersonaResumen?> ObtenerAsync(int idPersona)
    {
        var p = await Consulta().FirstOrDefaultAsync(x => x.IdPersona == idPersona);
        return p is null ? null : Mapear(p, false);
    }

    public async Task<PersonaResumen> RegistrarAsync(RegistrarPersonaRequest req, int idUsuario, string? ip)
    {
        var nombre = req.NombreCompleto?.Trim();
        if (string.IsNullOrEmpty(nombre))
            throw new NegocioException("El nombre completo es obligatorio.");
        if (string.IsNullOrWhiteSpace(req.DireccionExacta))
            throw new NegocioException("La dirección exacta es obligatoria.");
        if (req.FechaNacimiento > DateOnly.FromDateTime(DateTime.Today))
            throw new NegocioException("La fecha de nacimiento no puede ser futura.");

        var cedula = NormalizarCedula(req.Cedula);

        // RN017: si la cédula ya existe, se reutiliza la persona
        Persona? persona = null;
        if (cedula is not null)
        {
            persona = await _db.Personas
                .Include(p => p.Tutor).Include(p => p.Estudiante).Include(p => p.Docente)
                .FirstOrDefaultAsync(p => p.Cedula == cedula);
        }
        var reutilizada = persona is not null;

        var pideRoles = req.EsTutor || req.Estudiante is not null || req.Docente is not null;
        if (reutilizada && !pideRoles)
            throw new NegocioException("La persona ya está registrada. Seleccione un rol nuevo para asignarle.");

        if (persona is null)
        {
            if (!await _db.Barrios.AnyAsync(b => b.IdBarrio == req.IdBarrio))
                throw new NegocioException("El barrio seleccionado no existe.");

            persona = new Persona
            {
                NombreCompleto = nombre,
                FechaNacimiento = req.FechaNacimiento,
                Cedula = cedula,
                Celular = string.IsNullOrWhiteSpace(req.Celular) ? null : req.Celular.Trim(),
                IdBarrio = req.IdBarrio,
                DireccionExacta = req.DireccionExacta.Trim(),
                Estado = true
            };
            _db.Personas.Add(persona);
        }

        var roles = new List<string>();

        if (req.EsTutor)
        {
            if (persona.Tutor is not null)
                throw new NegocioException("La persona ya tiene el rol de tutor.");
            persona.Tutor = new Tutor { Estado = true };
            roles.Add("tutor");
        }

        if (req.Estudiante is not null)
        {
            if (persona.Estudiante is not null)
                throw new NegocioException("La persona ya tiene el rol de estudiante.");
            var idTutor = req.Estudiante.IdTutor;
            if (idTutor is not null && !await _db.Tutors.AnyAsync(t => t.IdTutor == idTutor))
                throw new NegocioException("El tutor seleccionado no existe.");
            // RN001 (menor sin tutor) y tutor inhabilitado los valida el trigger
            persona.Estudiante = new Estudiante { IdTutor = idTutor, Estado = true };
            roles.Add("estudiante");
        }

        if (req.Docente is not null)
        {
            if (persona.Docente is not null)
                throw new NegocioException("La persona ya tiene el rol de docente.");
            var d = req.Docente;
            if (d.AniosExperiencia < 0)
                throw new NegocioException("Los años de experiencia no pueden ser negativos.");
            var ids = (d.IdModalidades ?? new List<int>()).Distinct().ToList();
            if (ids.Count == 0)
                throw new NegocioException("Seleccione al menos una modalidad de disponibilidad para el docente.");
            var modalidades = await _db.Modalidads
                .Where(m => ids.Contains(m.IdModalidad) && m.Estado).ToListAsync();
            if (modalidades.Count != ids.Count)
                throw new NegocioException("Alguna modalidad seleccionada no existe o está inhabilitada.");

            persona.Docente = new Docente
            {
                EsLicenciado = d.EsLicenciado,
                AniosExperiencia = d.AniosExperiencia,
                InstitucionesLaborales = string.IsNullOrWhiteSpace(d.InstitucionesLaborales)
                    ? null : d.InstitucionesLaborales.Trim(),
                IdModalidads = modalidades,
                Estado = true
            };
            roles.Add("docente");
        }

        // Un solo SaveChanges: persona + roles en una sola transacción
        await _db.SaveChangesAsync();

        await _auditoria.RegistrarAsync(
            idUsuario,
            reutilizada ? "AGREGAR_ROL" : "CREAR_PERSONA",
            "persona", persona.IdPersona, ip,
            roles.Count == 0 ? "Persona sin roles" : "Roles: " + string.Join(", ", roles));

        var resumen = await Consulta().FirstAsync(x => x.IdPersona == persona.IdPersona);
        return Mapear(resumen, reutilizada);
    }

    public async Task<List<TutorItem>> ListarTutoresAsync(string? texto)
    {
        var q = _db.Tutors.AsNoTracking().Where(t => t.Estado);
        if (!string.IsNullOrWhiteSpace(texto))
        {
            var s = texto.Trim();
            q = q.Where(t => t.IdPersonaNavigation.NombreCompleto.Contains(s)
                          || (t.IdPersonaNavigation.Cedula != null && t.IdPersonaNavigation.Cedula.Contains(s)));
        }
        return await q.OrderBy(t => t.IdPersonaNavigation.NombreCompleto)
            .Select(t => new TutorItem(t.IdTutor, t.IdPersona,
                t.IdPersonaNavigation.NombreCompleto, t.IdPersonaNavigation.Cedula))
            .ToListAsync();
    }

    private IQueryable<Persona> Consulta() => _db.Personas.AsNoTracking()
        .Include(p => p.IdBarrioNavigation).ThenInclude(b => b.IdMunicipioNavigation)
        .Include(p => p.Tutor).Include(p => p.Estudiante).Include(p => p.Docente);

    private static PersonaResumen Mapear(Persona p, bool reutilizada) => new(
     p.IdPersona, p.NombreCompleto, p.FechaNacimiento, p.Cedula, p.Celular, p.DireccionExacta,
     p.IdBarrio, p.IdBarrioNavigation.IdMunicipio,
     p.IdBarrioNavigation.IdMunicipioNavigation.IdDepartamento,
     p.Tutor?.IdTutor, p.Tutor is not null, p.Estudiante is not null, p.Docente is not null,
     reutilizada, p.Estudiante?.IdTutor);

    private static string? NormalizarCedula(string? c) =>
        string.IsNullOrWhiteSpace(c) ? null : c.Trim().ToUpperInvariant();
}