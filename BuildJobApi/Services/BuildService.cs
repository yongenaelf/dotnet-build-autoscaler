using System.Diagnostics;
using System.IO.Compression;
using System.Text.RegularExpressions;
using BuildJobApi.Interfaces;

namespace BuildJobApi.Services;

public class BuildService(IHubCallerService hubCallerService, IObjectStorageService objectStorageService) : IBuildService
{
  public async Task ProcessBuild(string connectionId, string jobId, string command)
  {
    Console.WriteLine($"Starting: Job {jobId} with command {command}");

    if (new List<string> { "build", "test" }.Contains(command) == false)
    {
      Console.WriteLine("Neither build nor test. Skipping.");
      return;
    }

    Console.WriteLine($"Downloading {jobId} from object storage.");
    var stream = await objectStorageService.Get(jobId + ".zip");

    if (stream == null)
    {
      Console.WriteLine("File not found.");
      return;
    }

    Console.WriteLine($"Downloaded {jobId} from object storage.");

    var archive = new ZipArchive(stream);

    var extractPath = Path.Combine(Path.GetTempPath(), jobId);

    // if path exists, delete it
    if (Directory.Exists(extractPath))
    {
      Directory.Delete(extractPath, true);
    }

    Console.WriteLine($"Extracting to {extractPath}.");
    ZipFileExtensions.ExtractToDirectory(archive, extractPath);

    var extension = command == "test" ? ".Tests.csproj" : ".csproj";

    Console.WriteLine($"Searching for {extension} file in the extracted archive.");
    var csprojFile = Directory.GetFiles(extractPath, $"*{extension}", SearchOption.AllDirectories).FirstOrDefault();

    if (csprojFile == null)
    {
      Console.WriteLine($"No {extension} file found in the extracted archive.");
      return;
    }

    Console.WriteLine($"Starting dotnet ${command} for ${csprojFile}.");

    var processInfo = new ProcessStartInfo("dotnet", $"{command} \"{csprojFile}\"")
    {
      RedirectStandardOutput = true,
      RedirectStandardError = true,
      UseShellExecute = false,
      CreateNoWindow = true
    };

    using (var process = new Process { StartInfo = processInfo })
    {
      if (process == null)
      {
        Console.WriteLine("Failed to start the process.");
        return;
      }

      process.OutputDataReceived += (sender, args) =>
      {
        if (args.Data != null)
        {
          Console.WriteLine(args.Data);

          if (args.Data.Contains(".dll.patched"))
          {
            string pattern = @"Saving as (.+)$";
            string input = args.Data.Trim();
            var match = Regex.Match(input, pattern);

            var patchedDllPath = match.Groups[1].Value;

            if (patchedDllPath != null && File.Exists(patchedDllPath))
            {
              var patchedDllBytes = File.ReadAllBytes(patchedDllPath);
              var patchedDllBase64 = Convert.ToBase64String(patchedDllBytes);


              hubCallerService.SendMessageToUser(connectionId, $"Patched DLL: {patchedDllBase64}");
            }
            else
            {
              Console.WriteLine("Patched DLL not found.");
            }
          }
          else
          {
            hubCallerService.SendMessageToUser(connectionId, args.Data);
          }
        }
      };
      process.ErrorDataReceived += (sender, args) => Console.WriteLine(args.Data);

      process.Start();
      process.BeginOutputReadLine();
      process.BeginErrorReadLine();
      process.WaitForExit();
    }

    Console.WriteLine("Job completed.");
  }
}
