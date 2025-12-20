using Atto.LinearWrap;
using RAWSimO.Core.Statistics;
using System;
using System.Collections.Generic;
using System.IO;

namespace RAWSimO.DEA;

internal class Program
{
    private static void Main(string[] args)
    {
        // Init logging
        using (var sw = new StreamWriter("DEA.log", false))
        {
            // Create log action
            var log = (string msg) => { Console.Write(msg); sw.Write(msg); };

            // Say hello
            log("<<< Welcome to the RAWSimO DEA handler >>>" + Environment.NewLine);

            // Read path to footprints file
            string path;
            if (args.Length == 1)
            {
                path = args[0];
                log("Using footprints.csv from argument: " + path + Environment.NewLine);
            }
            else
            {
                log("Enter the path to the footprints.csv file:" + Environment.NewLine);
                path = Console.ReadLine();
            }
            sw.WriteLine(path);

            // Build default configurations to run
            var configurations = new List<DEAConfiguration> {
                new()
                {
                    Name = "Mu",
                    Datafile = path,
                    LogAction = log,
                    SolverChoice = SolverType.Gurobi,
                    InputOriented = false,
                    WeightsSumToOne = true,
                    TransformOutputOrientedEfficiency = false,
                    ResultFileCondensed = "deascoremu.csv",
                    BoxPlotBaseFilename = "deaboxplotsmu",
                    Groups =
                    [
                        FootprintDatapoint.FootPrintEntry.TagSetting1
                    ],
                    ServiceUnitIdents =
                    [
                        FootprintDatapoint.FootPrintEntry.NOStations,
                        //FootprintDatapoint.FootPrintEntry.NIStations,
                        FootprintDatapoint.FootPrintEntry.BotsPerOStation,
                        //FootprintDatapoint.FootPrintEntry.OStationCapacityAvg,
                        //FootprintDatapoint.FootPrintEntry.Controller,
                        FootprintDatapoint.FootPrintEntry.TA,
                        FootprintDatapoint.FootPrintEntry.IS,
                        FootprintDatapoint.FootPrintEntry.PS,
                        FootprintDatapoint.FootPrintEntry.RB,
                        FootprintDatapoint.FootPrintEntry.OB
                    ],
                    Inputs =
                    [
                        new(FootprintDatapoint.FootPrintEntry.NOStations, InputType.Resource),
                        //new Tuple<FootprintDatapoint.FootPrintEntry, InputType>(FootprintDatapoint.FootPrintEntry.NIStations, InputType.Resource),
                        new(FootprintDatapoint.FootPrintEntry.BotsPerOStation, InputType.Resource)
                        //new Tuple<FootprintDatapoint.FootPrintEntry, InputType>(FootprintDatapoint.FootPrintEntry.OStationCapacityAvg, InputType.Resource),
                    ],
                    Outputs =
                    [
                        new(FootprintDatapoint.FootPrintEntry.ItemThroughputRateScore, OutputType.Benefit),
                        new(FootprintDatapoint.FootPrintEntry.OrderLatenessAvg, OutputType.Loss),
                        new(FootprintDatapoint.FootPrintEntry.LateOrdersFractional, OutputType.Loss)
                    ],
                },
            };

            // Create and solve models
            foreach (var config in configurations)
            {
                log(">>> Creating model: " + config.Name + Environment.NewLine);
                var modelmu1 = new DEAModel(config);
                log(">>> Solving model: " + config.Name + Environment.NewLine);
                modelmu1.Solve();
            }
        }

        // Wait for it ....
        Console.WriteLine(".Fin.");
        Console.ReadLine();
    }
}