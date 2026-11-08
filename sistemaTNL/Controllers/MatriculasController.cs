using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;
using sistemaTNL.Interfaces;
using sistemaTNL.Services;

namespace sistemaTNL.Controllers;

// Cuerpo para inscribir a un estudiante (RF027)
public record CrearMatriculaRequest(int IdEstudiante, int IdGrupo, string? Observaciones);

// Cuerpo para modificar una matrícula (RF028): solo grupo y observaciones
public record ModificarMatriculaRequest(int IdGrupo, string? Observaciones);

// Forma en que devolvemos cada matrícula
public record MatriculaItem(
    int IdMatricula, int IdEstudiante, string? NumeroCarne, string Estudiante,
    int IdPeriodo, string Curso, string Modalidad,
    int IdGrupo, string Grupo, string Nivel,
    DateOnly FechaMatricula, string? Observaciones,
    bool Estado, DateOnly? FechaInhabilitacion);

// Cada cobro que se genera al matricular (matrícula, certificado, mensualidades...)
public record ObligacionItem(
    int IdObligacion, string Descripcion, DateOnly? Mes,
    decimal? PrecioFinalUsd, decimal TotalAbonadoUsd, decimal? SaldoPendienteUsd, string Estado);

[ApiController]
[Route("api/matriculas")]
public class MatriculasController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IAuditoriaService _auditoria;

    public MatriculasController(ApplicationDbContext db, IAuditoriaService auditoria)
    {
        _db = db;
        _auditoria = auditoria;
    }

    // GET api/matriculas/estudiantes?buscar=jos
    // Busca estudiantes habilitados por nombre, cédula o carné, para elegir a quién matricular
    [HttpGet("estudiantes")]
    public async Task<IActionResult> BuscarEstudiantes([FromQuery] string? buscar)
    {
        var q = _db.Estudiantes.AsNoTracking().Where(e => e.Estado);

        if (!string.IsNullOrWhiteSpace(buscar))
        {
            var s = buscar.Trim();
            q = q.Where(e =>
                e.IdPersonaNavigation.NombreCompleto.Contains(s) ||
                (e.NumeroCarne != null && e.NumeroCarne.Contains(s)) ||
                (e.IdPersonaNavigation.Cedula != null && e.IdPersonaNavigation.Cedula.Contains(s)));
        }

        return Ok(await q
            .OrderBy(e => e.IdPersonaNavigation.NombreCompleto)
            .Select(e => new
            {
                e.IdEstudiante,
                Nombre = e.IdPersonaNavigation.NombreCompleto,
                e.NumeroCarne,
                Cedula = e.IdPersonaNavigation.Cedula
            })
            .Take(50)
            .ToListAsync());
    }

    // GET api/matriculas?idPeriodo=1&idGrupo=1&idEstudiante=1  (todos los filtros son opcionales)
    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] int? idPeriodo, [FromQuery] int? idGrupo, [FromQuery] int? idEstudiante)
    {
        var q = _db.Matriculas.AsNoTracking().AsQueryable();
        if (idPeriodo is not null) q = q.Where(m => m.IdPeriodo == idPeriodo);
        if (idGrupo is not null) q = q.Where(m => m.IdGrupo == idGrupo);
        if (idEstudiante is not null) q = q.Where(m => m.IdEstudiante == idEstudiante);

        return Ok(await Proyectar(q).OrderByDescending(m => m.IdMatricula).ToListAsync());
    }

    // GET api/matriculas/{id}
    // Devuelve la matrícula junto con sus obligaciones de pago
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obtener(int id)
    {
        var detalle = await DetalleAsync(id);
        return detalle is null
            ? NotFound(new { mensaje = "La matrícula no existe." })
            : Ok(detalle);
    }

    // POST api/matriculas  (RF027)
    // Ejemplo: { "idEstudiante": 2, "idGrupo": 1, "observaciones": null }
    [HttpPost]
    public async Task<IActionResult> Crear(CrearMatriculaRequest req)
    {
        // Mensajes claros para los casos más comunes
        var estudiante = await _db.Estudiantes.AsNoTracking()
            .FirstOrDefaultAsync(e => e.IdEstudiante == req.IdEstudiante)
            ?? throw new NegocioException("El estudiante no está registrado. Primero debe registrarse.");
        if (!estudiante.Estado)
            throw new NegocioException("El estudiante está inhabilitado.");
        if (!await _db.Grupos.AnyAsync(g => g.IdGrupo == req.IdGrupo))
            throw new NegocioException("El grupo seleccionado no existe.");

        var observaciones = string.IsNullOrWhiteSpace(req.Observaciones) ? null : req.Observaciones.Trim();

        // Llamamos al procedimiento de la base. Devuelve dos valores por parámetros de salida.
        // Si incumple una regla (período no vigente, grupo lleno, matrícula repetida...),
        // la base lanza un error que el middleware convierte en un 400 con el mensaje.
        var pMatricula = new SqlParameter("@id_matricula", SqlDbType.Int)
        { Direction = ParameterDirection.Output };
        var pCarne = new SqlParameter("@numero_carne", SqlDbType.Char, 7)
        { Direction = ParameterDirection.Output };

        await _db.Database.ExecuteSqlRawAsync(
            "EXEC dbo.sp_registrar_matricula @id_estudiante, @id_grupo, @observaciones, " +
            "@id_matricula OUTPUT, @numero_carne OUTPUT",
            new SqlParameter("@id_estudiante", req.IdEstudiante),
            new SqlParameter("@id_grupo", req.IdGrupo),
            new SqlParameter("@observaciones", (object?)observaciones ?? DBNull.Value),
            pMatricula, pCarne);

        var idMatricula = (int)pMatricula.Value;

        await AuditarAsync("CREAR_MATRICULA", idMatricula, $"Carné {pCarne.Value}");

        return Created($"/api/matriculas/{idMatricula}", await DetalleAsync(idMatricula));
    }

    // PUT api/matriculas/{id}  (RF028)
    // Solo permite cambiar de grupo (dentro del mismo período) y las observaciones
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Modificar(int id, ModificarMatriculaRequest req)
    {
        var matricula = await _db.Matriculas.FirstOrDefaultAsync(m => m.IdMatricula == id)
            ?? throw new NegocioException("La matrícula no existe.");

        if (!matricula.Estado)
            throw new NegocioException("Una matrícula inhabilitada no puede modificarse.");

        var grupo = await _db.Grupos.AsNoTracking().FirstOrDefaultAsync(g => g.IdGrupo == req.IdGrupo)
            ?? throw new NegocioException("El grupo seleccionado no existe.");

        // El nuevo grupo debe ser del mismo período de la matrícula
        if (grupo.IdPeriodo != matricula.IdPeriodo)
            throw new NegocioException("El grupo seleccionado no corresponde al período de la matrícula.");

        // Solo validamos que esté habilitado si realmente cambia de grupo
        if (grupo.IdGrupo != matricula.IdGrupo && !grupo.Estado)
            throw new NegocioException("El grupo seleccionado está inhabilitado.");

        matricula.IdGrupo = req.IdGrupo;
        matricula.Observaciones = string.IsNullOrWhiteSpace(req.Observaciones)
            ? null : req.Observaciones.Trim();

        // El trigger valida la capacidad máxima del nuevo grupo (RN031)
        await _db.SaveChangesAsync();

        await AuditarAsync("EDITAR_MATRICULA", matricula.IdMatricula, "Matrícula modificada");

        return Ok(await DetalleAsync(matricula.IdMatricula));
    }

    // PUT api/matriculas/{id}/inhabilitar  (RF029)
    // No lleva cuerpo. La matrícula no se borra y el estudiante no cambia de estado.
    [HttpPut("{id:int}/inhabilitar")]
    public async Task<IActionResult> Inhabilitar(int id)
    {
        var matricula = await _db.Matriculas.FirstOrDefaultAsync(m => m.IdMatricula == id)
            ?? throw new NegocioException("La matrícula no existe.");

        if (!matricula.Estado)
            throw new NegocioException("La matrícula ya está inhabilitada.");

        matricula.Estado = false;
        // El trigger de la base fija la fecha de inhabilitación
        await _db.SaveChangesAsync();

        await AuditarAsync("INHABILITAR_MATRICULA", matricula.IdMatricula, "Matrícula inhabilitada");

        return Ok(await DetalleAsync(matricula.IdMatricula));
    }

    // ---------- Apoyo ----------

    // Matrícula con sus obligaciones de pago
    private async Task<object?> DetalleAsync(int idMatricula)
    {
        var matricula = await Proyectar(_db.Matriculas.AsNoTracking().Where(m => m.IdMatricula == idMatricula))
            .FirstOrDefaultAsync();
        if (matricula is null) return null;

        var obligaciones = await _db.ObligacionPagos.AsNoTracking()
            .Where(o => o.IdMatricula == idMatricula && o.EstadoPago != "Anulado")
            .OrderBy(o => o.MesCorrespondiente).ThenBy(o => o.IdObligacion)
            .Select(o => new ObligacionItem(
                o.IdObligacion, o.Descripcion, o.MesCorrespondiente,
                o.PrecioFinalUsd, o.TotalAbonadoUsd, o.SaldoPendienteUsd, o.EstadoPago))
            .ToListAsync();

        return new { matricula, obligaciones };
    }

    // Convierte matrículas en MatriculaItem, trayendo los nombres de estudiante, curso, grupo...
    private static IQueryable<MatriculaItem> Proyectar(IQueryable<Entities.Matricula> q) =>
        q.Select(m => new MatriculaItem(
            m.IdMatricula,
            m.IdEstudiante,
            m.IdEstudianteNavigation.NumeroCarne,
            m.IdEstudianteNavigation.IdPersonaNavigation.NombreCompleto,
            m.IdPeriodo,
            m.IdPeriodoNavigation.IdCursoNavigation.Nombre,
            m.IdPeriodoNavigation.IdModalidadNavigation.Nombre,
            m.IdGrupo,
            m.Grupo.Identificacion,
            m.Grupo.IdNivelNavigation.Nombre,
            m.FechaMatricula,
            m.Observaciones,
            m.Estado,
            m.FechaInhabilitacion));

    // Auditoría: quién hizo el cambio y desde qué IP
    private async Task AuditarAsync(string accion, int idMatricula, string descripcion)
    {
        var idUsuario = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        await _auditoria.RegistrarAsync(idUsuario, accion, "matricula", idMatricula,
            HttpContext.Connection.RemoteIpAddress?.ToString(), descripcion);
    }
}