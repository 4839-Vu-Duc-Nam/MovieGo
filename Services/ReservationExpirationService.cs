using Microsoft.Data.SqlClient;
using MovieGoApp.Data;

namespace MovieGoApp.Services;

public sealed class ReservationExpirationService(
    IServiceScopeFactory scopeFactory,
    ILogger<ReservationExpirationService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var store = scope.ServiceProvider.GetRequiredService<MovieGoStore>();
                await store.ExpireReservationsAsync(stoppingToken);
            }
            catch (SqlException ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "MovieGo could not expire overdue seat reservations.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        } while (!stoppingToken.IsCancellationRequested);
    }
}
