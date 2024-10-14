namespace BuildJobApi.Interfaces;

public interface IHubCallerService
{
  Task SendMessageToGroup(string group, string message);
  Task SendMessageToUser(string connectionId, string message);
}