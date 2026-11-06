namespace sistemaTNL.Interfaces;

public interface IAuditoriaService
{
    Task RegistrarAsync(int idUsuario, string tipoAccion, string entidad,
                        int? idRegistro, string? ip, string? descripcion);
}