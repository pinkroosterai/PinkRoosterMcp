namespace PinkRooster.Api.Services;

public sealed class WebhookDeliveryBackgroundService(
    WebhookEventChannel channel,
    IServiceScopeFactory scopeFactory,
    ILogger<WebhookDeliveryBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (await channel.Reader.WaitToReadAsync(stoppingToken))
                {
                    while (channel.Reader.TryRead(out var evt))
                    {
                        try
                        {
                            await using var scope = scopeFactory.CreateAsyncScope();
                            var webhookService = scope.ServiceProvider.GetRequiredService<IWebhookService>();
                            await webhookService.EnqueueDeliveryAsync(
                                evt.ProjectId, evt.EventType, evt.EntityType, evt.EntityId, evt.Payload, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to deliver webhook for {EventType} {EntityId}",
                                evt.EventType, evt.EntityId);
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error in webhook delivery background service");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }
}
