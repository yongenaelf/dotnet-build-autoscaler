namespace BuildJobShared.Interfaces;

public interface IKafkaService
{
  Task PublishJobRequest(string jobId, string command);
  Task SubscribeJobLog(Action<string> callback);
  Task SubscribeJobResult(Action<string> callback);
}