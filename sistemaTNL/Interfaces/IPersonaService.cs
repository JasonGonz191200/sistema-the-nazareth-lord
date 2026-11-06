using sistemaTNL.DTOs;

namespace sistemaTNL.Interfaces;

public interface IPersonaService
{
    Task<PersonaResumen?> BuscarPorCedulaAsync(string cedula);
    Task<PersonaResumen?> ObtenerAsync(int idPersona);
    Task<PersonaResumen> RegistrarAsync(RegistrarPersonaRequest req, int idUsuario, string? ip);
    Task<List<TutorItem>> ListarTutoresAsync(string? texto);
}