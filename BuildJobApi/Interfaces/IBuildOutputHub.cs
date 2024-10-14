namespace BuildJobApi.Interfaces;

public interface IBuildOutputHub
{
  Task ReceiveMessage(string message);
  Task StartBuild(string command);
}