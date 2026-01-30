using Atto.LinearWrap;
using System;
using System.IO;
using System.Linq;

namespace RAWSimO.MDPSolve;

internal class Program
{
    private static void Main(string[] args)
    {
        //string instanceFile = Path.Combine("..", "..", "..", "..", "Material", "MDP", "testCase1_memoryEfficient.txt");
        Console.WriteLine("<<< Welcome to the RAWSimO MDP solver >>>");
        string instanceFile;
        if (args.Length >= 1)
        {
            instanceFile = args[0];
        }
        else
        {
            Console.WriteLine("Specify input file: ");
            instanceFile = Console.ReadLine();
        }
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var extension = instanceFile.EndsWith(".txt.gz") ? ".txt.gz" : Path.GetExtension(instanceFile);
        var instanceName = instanceFile.EndsWith(".txt.gz") ? Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(instanceFile)) : Path.GetFileNameWithoutExtension(instanceFile);
        var solutionFile = Path.Combine(Path.GetDirectoryName(instanceFile), instanceName + "." + timestamp + ".solution" + extension);
        var logFile = Path.Combine(Path.GetDirectoryName(instanceFile), instanceName + "." + timestamp + ".log" + extension);
        var model = new MDPLP(instanceFile, (string msg) => { Console.Write(msg); }, SolverType.CPLEX, args.Skip(1).ToArray());
        model.Solve();
        if (model.SolutionAvailable)
            model.Write(solutionFile);
        model.DumpLog(logFile);
        Console.WriteLine(".Fin.");
        if (args.Length == 0)
            Console.ReadLine();
    }
}