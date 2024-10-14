namespace Shared.Interfaces;

public interface IEventPublishService
{
  Task PublishAsync<T>(string topic, T message, string? key = null);
}