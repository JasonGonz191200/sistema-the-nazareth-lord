using Microsoft.Data.SqlClient;
using sistemaTNL.Services;

namespace sistemaTNL.Middleware;

public class ManejadorErroresMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ManejadorErroresMiddleware> _log;

    public ManejadorErroresMiddleware(RequestDelegate next, ILogger<ManejadorErroresMiddleware> log)
    {
        _next = next;
        _log = log;
    }

    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await _next(ctx);
        }
        catch (Exception ex) when (!ctx.Response.HasStarted)
        {
            var sql = BuscarSqlException(ex);
            if (sql is not null)
                foreach (SqlError e in sql.Errors)
                    _log.LogWarning("SQL {Numero}: {Mensaje}", e.Number, e.Message);
            int estado;
            string mensaje;

            if (ex is NegocioException negocio)
            {
                estado = StatusCodes.Status400BadRequest;
                mensaje = negocio.Message;
            }
            else if (sql is not null && sql.Number is >= 50001 and <= 50099)
            {
                // Regla de negocio lanzada desde la base (THROW): el mensaje ya es para el usuario
                estado = StatusCodes.Status400BadRequest;
                mensaje = sql.Message;
            }
            else if (sql is not null && sql.Number is 2627 or 2601)
            {
                estado = StatusCodes.Status409Conflict;
                mensaje = "Ya existe un registro con esos datos.";
            }
            else if (sql is not null && sql.Number == 547)
            {
                estado = StatusCodes.Status400BadRequest;
                mensaje = "Los datos no cumplen las restricciones de integridad del sistema.";
            }
            else
            {
                // Error inesperado: se registra en el log y no se expone el detalle (RNF002, RNF009)
                _log.LogError(ex, "Error no controlado en {Metodo} {Ruta}", ctx.Request.Method, ctx.Request.Path);
                estado = StatusCodes.Status500InternalServerError;
                mensaje = "Ocurrió un error inesperado. La operación no se completó.";
            }

            ctx.Response.StatusCode = estado;
            await ctx.Response.WriteAsJsonAsync(new { mensaje });
        }
    }

    // El error de SQL suele venir envuelto en DbUpdateException u otras excepciones
    private static SqlException? BuscarSqlException(Exception? ex)
    {
        while (ex is not null)
        {
            if (ex is SqlException s) return s;
            ex = ex.InnerException;
        }
        return null;
    }
}