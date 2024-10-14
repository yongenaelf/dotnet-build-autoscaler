namespace BuildJobApi.Interfaces;

public interface IBuildOutputHub
{
  Task ReceiveMessage(string message);
}