using Microsoft.EntityFrameworkCore;
using sistemaTNL.Data;

namespace sistemaTNL.Services;

// Tarea en segundo plano: pasa a "Finalizado" los períodos habilitados
// cuya fecha de fin de curso ya pasó (RN012).
// Corre al iniciar la aplicación y luego cada hora; el procedimiento es seguro
// de repetir porque solo afecta períodos que todavía están "Habilitado".
public class FinalizarPeriodosService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<FinalizarPeriodosService> _logger;

    // Cada cuánto se revisa
    private static readonly TimeSpan Intervalo = TimeSpan.FromHours(1);

    public FinalizarPeriodosService(IServiceScopeFactory scopeFactory, ILogger<FinalizarPeriodosService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // El DbContext es "scoped", por eso se crea un ámbito en cada vuelta
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                await db.Database.ExecuteSqlRawAsync("EXEC dbo.sp_finalizar_periodos", stoppingToken);
                _logger.LogInformation("Revisión de períodos a finalizar ejecutada.");
            }
            catch (OperationCanceledException)
            {
                // La aplicación se está deteniendo: salir sin error
                break;
            }
            catch (Exception ex)
            {
                // Un fallo no debe tumbar la aplicación; se registra y se reintenta en la próxima vuelta
                _logger.LogError(ex, "Error al finalizar períodos.");
            }

            try
            {
                await Task.Delay(Intervalo, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}