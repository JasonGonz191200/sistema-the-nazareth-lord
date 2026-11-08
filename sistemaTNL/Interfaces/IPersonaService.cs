using sistemaTNL.DTOs;

namespace sistemaTNL.Interfaces;

public interface IPersonaService
{
    Task<PersonaResumen?> BuscarPorCedulaAsync(string cedula);
    Task<PersonaResumen?> ObtenerAsync(int idPersona);
    Task<PersonaResumen> RegistrarAsync(RegistrarPersonaRequest req, int idUsuario, string? ip);
    Task<List<TutorItem>> ListarTutoresAsync(string? texto);
    Task<PersonaResumen> ActualizarAsync(int idPersona, ActualizarPersonaRequest req, int idUsuario, string? ip);
    // Habilita o inhabilita un rol de la persona ("estudiante", "tutor" o "docente")
    Task<RolEstadoResultado> CambiarEstadoRolAsync(int idPersona, string rol, bool habilitado, int idUsuario, string? ip);
}