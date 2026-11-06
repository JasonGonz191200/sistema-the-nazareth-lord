using sistemaTNL.Data;
using sistemaTNL.Entities;
using sistemaTNL.Interfaces;

namespace sistemaTNL.Services;

public class AuditoriaService : IAuditoriaService
{
    private readonly ApplicationDbContext _db;
    public AuditoriaService(ApplicationDbContext db) => _db = db;

    public async Task RegistrarAsync(int idUsuario, string tipoAccion, string entidad,
                                     int? idRegistro, string? ip, string? descripcion)
    {
        _db.RegistroAuditoria.Add(new RegistroAuditorium
        {
            IdUsuario = idUsuario,
            FechaHora = DateTimeOffset.Now,
            TipoAccion = tipoAccion,
            EntidadAfectada = entidad,
            IdRegistroAfectado = idRegistro,
            Resultado = "Exitoso",
            DireccionIp = ip,
            Descripcion = descripcion
        });
        await _db.SaveChangesAsync();
    }
}