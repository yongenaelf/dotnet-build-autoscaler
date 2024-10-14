namespace BuildJobApi.Interfaces;

public interface IBuildService
{
  Task ProcessBuild(string connectionId, string jobId, string command);
}