using System.Diagnostics;

namespace Frigorino.IntegrationTests.Infrastructure;

public static class SpaBuildHelper
{
    public static void EnsureSpaIsBuilt()
    {
        var webRoot = FindWebProjectRoot();
        var clientAppDir = Path.Combine(webRoot, "ClientApp");
        var indexHtml = Path.Combine(clientAppDir, "build", "index.html");

        if (IsBuildCurrent(clientAppDir, indexHtml))
            return;

        Console.WriteLine("[SpaBuildHelper] SPA build is stale or missing — running npm run build...");
        RunProcess("npm", "run build", clientAppDir);
    }

    private static bool IsBuildCurrent(string clientAppDir, string indexHtml)
    {
        if (!File.Exists(indexHtml))
            return false;

        var buildTime = File.GetLastWriteTimeUtc(indexHtml);
        var srcDir = Path.Combine(clientAppDir, "src");

        if (!Directory.Exists(srcDir))
            return true;

        return !Directory.EnumerateFiles(srcDir, "*", SearchOption.AllDirectories)
            .Any(f => File.GetLastWriteTimeUtc(f) > buildTime);
    }

    private static void RunProcess(string fileName, string arguments, string workingDir)
    {
        // On Windows, npm is a .cmd script — must route through cmd.exe
        if (OperatingSystem.IsWindows())
        {
            arguments = $"/c {fileName} {arguments}";
            fileName = "cmd.exe";
        }

        var psi = new ProcessStartInfo(fileName, arguments)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = false,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start process: {fileName}");

        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var stderr = process.StandardError.ReadToEnd();
            throw new InvalidOperationException($"npm run build failed (exit {process.ExitCode}): {stderr}");
        }
    }

    internal static string FindWebProjectRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "Frigorino.Web");
            if (Directory.Exists(candidate) && File.Exists(Path.Combine(candidate, "Frigorino.Web.csproj")))
                return candidate;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException(
            $"Could not locate Frigorino.Web project directory. Search started from: {AppContext.BaseDirectory}");
    }
}
