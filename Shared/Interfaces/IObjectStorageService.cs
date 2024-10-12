namespace Shared.Interfaces;

public interface IObjectStorageService
{
  Task<Stream?> Get(string key);
  Task Put(string key, string contentType, Stream stream);
  Task Delete(string key);
}