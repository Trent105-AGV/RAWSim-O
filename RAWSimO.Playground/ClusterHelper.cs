using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace RAWSimO.Playground;

internal class ClusterHelper
{
    /// <summary>
    /// Sends the binaries to the cluster.
    /// </summary>
    public static void SendBinDirToCluster()
    {
        var process = new Process();
        var startInfo = new ProcessStartInfo();
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.FileName = "pscp.exe";
        var sources = // TODO find a way to transfer only the sources not the binaries
            Directory.EnumerateFiles(Path.Combine("..", "..", "..", ".."), "*.cs", SearchOption.AllDirectories).Concat(
                Directory.EnumerateFiles(Path.Combine("..", "..", "..", ".."), "*.sln", SearchOption.AllDirectories)).Concat(
                Directory.EnumerateFiles(Path.Combine("..", "..", "..", ".."), "*.csproj", SearchOption.AllDirectories)).Concat(
                Directory.EnumerateFiles(Path.Combine("..", "..", "..", ".."), "*.sh", SearchOption.AllDirectories)).Concat(
                Directory.EnumerateFiles(Path.Combine("..", "..", "..", ".."), "*.config", SearchOption.AllDirectories)).ToArray();
        sources = sources.Where(file => !(file.Contains("obj") || file.Contains("bin"))).ToArray();
        startInfo.Arguments =
            "-r " + // Enable option for copying folders
            Path.Combine("..", "..", "..", "..", "*") // From directory
            + " " // Delimiter
            + "mmarius@fe.pc2.upb.de:/scratch/mmarius/research/awsimopt/bin"; // To cluster directory
        Console.WriteLine("Executing " + startInfo.FileName + " with arguments: \n" + startInfo.Arguments);
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();
    }

    /// <summary>
    /// Sends the scripts to the cluster.
    /// </summary>
    public static void SendScriptDirToCluster()
    {
        var process = new Process();
        var startInfo = new ProcessStartInfo();
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.FileName = "pscp.exe";
        startInfo.Arguments =
            "-r " + // Enable option for copying folders
            Path.Combine("..", "..", "..", "..", "Material", "Scripts", "*") // From directory
            + " " // Delimiter
            + "mmarius@fe.pc2.upb.de:/scratch/mmarius/research/awsimopt/scripts"; // To cluster directory
        Console.WriteLine("Executing " + startInfo.FileName + " with arguments: \n" + startInfo.Arguments);
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();
    }

    /// <summary>
    /// Sends the instances to the cluster.
    /// </summary>
    public static void SendInstanceDirToCluster()
    {
        var process = new Process();
        var startInfo = new ProcessStartInfo();
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.FileName = "pscp.exe";
        startInfo.Arguments =
            "-r " + // Enable option for copying folders
            Path.Combine("..", "..", "..", "..", "Material", "Instances", "*") // From directory
            + " " // Delimiter
            + "mmarius@fe.pc2.upb.de:/scratch/mmarius/research/awsimopt/instances"; // To cluster directory
        Console.WriteLine("Executing " + startInfo.FileName + " with arguments: \n" + startInfo.Arguments);
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();
    }

    /// <summary>
    /// Sends the resources to the cluster.
    /// </summary>
    public static void SendResourceDirToCluster()
    {
        var process = new Process();
        var startInfo = new ProcessStartInfo();
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.FileName = "pscp.exe";
        startInfo.Arguments =
            "-r " + // Enable option for copying folders
            Path.Combine("..", "..", "..", "..", "Material", "Resources", "*") // From directory
            + " " // Delimiter
            + "mmarius@fe.pc2.upb.de:/scratch/mmarius/research/awsimopt/resources"; // To cluster directory
        Console.WriteLine("Executing " + startInfo.FileName + " with arguments: \n" + startInfo.Arguments);
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();
    }

    /// <summary>
    /// Fetches the results from the cluster.
    /// </summary>
    public static void FetchResultsFromCluster()
    {
        var fetchDir = Path.Combine("..", "..", "..", "..", "Material", "Results");
        if (!Directory.Exists(fetchDir))
            Directory.CreateDirectory(fetchDir);
        Console.WriteLine("Clear directory before fetching data? (yes/no)");
        var answer = Console.ReadLine();
        if (answer.Trim().StartsWith("Y") || answer.Trim().StartsWith("y"))
        {
            var fileCount = 0; var dirCount = 0;
            foreach (var dir in Directory.EnumerateDirectories(fetchDir))
            {
                Directory.Delete(dir, true);
                dirCount++;
            }
            foreach (var file in Directory.EnumerateFiles(fetchDir))
            {
                File.Delete(file);
                fileCount++;
            }
            Console.WriteLine("Deleted " + dirCount + " directories and " + fileCount + " files.");
        }
        var process = new Process();
        var startInfo = new ProcessStartInfo();
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.FileName = "pscp.exe";
        startInfo.Arguments =
            "-r " // Enable option for copying folders
            + "mmarius@fe.pc2.upb.de:/scratch/mmarius/research/awsimopt/output/*" // From cluster directory
            + " " // Delimiter
            + fetchDir; // To directory
        Console.WriteLine("Executing " + startInfo.FileName + " with arguments: \n" + startInfo.Arguments);
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();
    }

    /// <summary>
    /// Fetches the output files from the cluster.
    /// </summary>
    public static void FetchOutputFilesFromCluster()
    {
        var fetchDir = Path.Combine("..", "..", "..", "..", "Material", "Logs");
        if (!Directory.Exists(fetchDir))
            Directory.CreateDirectory(fetchDir);
        Console.WriteLine("Clear directory before fetching data? (y/n)");
        var answer = Console.ReadLine();
        if (answer.Trim().StartsWith("Y") || answer.Trim().StartsWith("y"))
        {
            var fileCount = 0; var dirCount = 0;
            foreach (var dir in Directory.EnumerateDirectories(fetchDir))
            {
                Directory.Delete(dir, true);
                dirCount++;
            }
            foreach (var file in Directory.EnumerateFiles(fetchDir))
            {
                File.Delete(file);
                fileCount++;
            }
            Console.WriteLine("Deleted " + dirCount + " directories and " + fileCount + " files.");
        }
        var process = new Process();
        var startInfo = new ProcessStartInfo();
        startInfo.WindowStyle = ProcessWindowStyle.Normal;
        startInfo.FileName = "pscp.exe";
        startInfo.Arguments =
            "-r " // Enable option for copying folders
            + "mmarius@fe.pc2.upb.de:/scratch/mmarius/tmp/*" // From cluster directory
            + " " // Delimiter
            + fetchDir; // To directory
        Console.WriteLine("Executing " + startInfo.FileName + " with arguments: \n" + startInfo.Arguments);
        process.StartInfo = startInfo;
        process.Start();
        process.WaitForExit();
    }
}