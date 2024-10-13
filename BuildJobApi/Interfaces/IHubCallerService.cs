namespace BuildJobApi.Interfaces;

public interface IHubCallerService
{
  Task SendMessage(string message);
}