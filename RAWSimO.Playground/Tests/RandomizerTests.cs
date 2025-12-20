using RAWSimO.Core.Configurations;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.IO;
using RAWSimO.Core.Randomization;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RAWSimO.Playground.Tests;

public class RandomizerTests
{
    #region Poisson tests

    public static void TestBasicPoisson()
    {
        var hours = 48;
        double currentTime = 0;
        var dueTime = TimeSpan.FromHours(hours).TotalSeconds;

        // Test inhomogeneous poisson generator
        var rate = PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromHours(1), 100);
        var generator = new PoissonGenerator(new RandomizerSimple(0), rate);
        var homogeneousSteps = new List<double>();
        while (currentTime < dueTime)
        {
            currentTime += generator.Next(currentTime);
            homogeneousSteps.Add(currentTime);
        }
        Console.WriteLine("Homogeneous Poisson generated " + homogeneousSteps.Count + " in " + hours + " seconds with a rate of " + rate);

        // Test inhomogeneous poisson generator
        currentTime = 0;
        var inhomogeneousGenerator = new PoissonGenerator(
            new RandomizerSimple(0),
            TimeSpan.FromHours(24).TotalSeconds,
            [
                new(TimeSpan.FromHours(0).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 20)),
                new(TimeSpan.FromHours(1).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 10)),
                new(TimeSpan.FromHours(2).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 5)),
                new(TimeSpan.FromHours(3).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 5)),
                new(TimeSpan.FromHours(4).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 10)),
                new(TimeSpan.FromHours(5).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 10)),
                new(TimeSpan.FromHours(6).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 20)),
                new(TimeSpan.FromHours(7).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 20)),
                new(TimeSpan.FromHours(8).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 40)),
                new(TimeSpan.FromHours(9).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 80)),
                new(TimeSpan.FromHours(10).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 80)),
                new(TimeSpan.FromHours(11).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 90)),
                new(TimeSpan.FromHours(12).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 110)),
                new(TimeSpan.FromHours(13).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 80)),
                new(TimeSpan.FromHours(14).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 90)),
                new(TimeSpan.FromHours(15).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 130)),
                new(TimeSpan.FromHours(16).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 180)),
                new(TimeSpan.FromHours(17).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 120)),
                new(TimeSpan.FromHours(18).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 190)),
                new(TimeSpan.FromHours(19).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 250)),
                new(TimeSpan.FromHours(20).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 220)),
                new(TimeSpan.FromHours(21).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 150)),
                new(TimeSpan.FromHours(22).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 110)),
                new(TimeSpan.FromHours(23).TotalSeconds, PoissonGenerator.TranslateIntoRateParameter(TimeSpan.FromMinutes(60), 50))
            ]);
        var inhomogeneousSteps = new List<double>();
        while (currentTime < dueTime)
        {
            currentTime += inhomogeneousGenerator.Next(currentTime);
            inhomogeneousSteps.Add(currentTime);
        }
        Console.WriteLine("Homogeneous Poisson generated " + inhomogeneousSteps.Count + " in " + hours + " with rates " + string.Join(",", inhomogeneousGenerator.TimeDependentRates.Select(r => "(" + r.Key + "/" + r.Value + ")")));

        // Output graph
        WriteHourBasedGraph([homogeneousSteps, inhomogeneousSteps], ["Homogeneous", "Inhomogeneous"], hours);
    }

    public static void TestInputTranslationTimeDependentPoisson()
    {
        var config = new SettingConfiguration();
        config.InventoryConfiguration.PoissonInventoryConfiguration = new PoissonInventoryConfiguration(new DefaultConstructorIdentificationClass());
        IRandomizer randomizer = new RandomizerSimple(0);
        var oStationCount = 3;
        var iStationCount = 3;
        // --> Instantiate poisson generator for orders
        // Calculate instance-specific factor to adapt the rates
        var relativeOrderWeights = new List<KeyValuePair<double, double>>();
        for (var i = 0; i < config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentOrderWeights.Count; i++)
        {
            relativeOrderWeights.Add(new KeyValuePair<double, double>(
                i > 0 ?
                    config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentOrderWeights[i].Key - config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentOrderWeights[i - 1].Key :
                    config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentOrderWeights[i].Key,
                config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentOrderWeights[i].Value
            ));
        }
        var unadjustedAverageOrderFrequency =
            relativeOrderWeights.Sum(w => w.Key * w.Value) /
            config.InventoryConfiguration.PoissonInventoryConfiguration.MaxTimeForTimeDependentOrderRates;
        var aimedAverageOrderFrequency =
            TimeSpan.FromSeconds(config.InventoryConfiguration.PoissonInventoryConfiguration.MaxTimeForTimeDependentOrderRates).TotalHours *
            config.InventoryConfiguration.PoissonInventoryConfiguration.AverageOrdersPerHourAndStation * oStationCount / config.InventoryConfiguration.PoissonInventoryConfiguration.MaxTimeForTimeDependentOrderRates;
        var orderSteerFactor = aimedAverageOrderFrequency / unadjustedAverageOrderFrequency;
        // Initiate order poisson generator
        var TimeDependentOrderPoissonGenerator = new PoissonGenerator(
            randomizer,
            config.InventoryConfiguration.PoissonInventoryConfiguration.MaxTimeForTimeDependentOrderRates,
            config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentOrderWeights.Select(w =>
                new KeyValuePair<double, double>(w.Key, orderSteerFactor * w.Value)));
        // --> Instantiate poisson generator for bundles
        // Calculate instance-specific factor to adapt the rates
        var relativeBundleWeights = new List<KeyValuePair<double, double>>();
        for (var i = 0; i < config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentBundleWeights.Count; i++)
        {
            relativeBundleWeights.Add(new KeyValuePair<double, double>(
                i > 0 ?
                    config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentBundleWeights[i].Key - config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentBundleWeights[i - 1].Key :
                    config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentBundleWeights[i].Key,
                config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentBundleWeights[i].Value
            ));
        }
        var unadjustedAverageBundleFrequency =
            relativeBundleWeights.Sum(w => w.Key * w.Value) /
            config.InventoryConfiguration.PoissonInventoryConfiguration.MaxTimeForTimeDependentBundleRates;
        var aimedAverageBundleFrequency =
            TimeSpan.FromSeconds(config.InventoryConfiguration.PoissonInventoryConfiguration.MaxTimeForTimeDependentBundleRates).TotalHours *
            config.InventoryConfiguration.PoissonInventoryConfiguration.AverageBundlesPerHourAndStation * iStationCount / config.InventoryConfiguration.PoissonInventoryConfiguration.MaxTimeForTimeDependentBundleRates;
        var bundleSteerFactor = aimedAverageBundleFrequency / unadjustedAverageBundleFrequency;
        // Initiate bundle poisson generator
        var TimeDependentBundlePoissonGenerator = new PoissonGenerator(
            randomizer,
            config.InventoryConfiguration.PoissonInventoryConfiguration.MaxTimeForTimeDependentBundleRates,
            config.InventoryConfiguration.PoissonInventoryConfiguration.TimeDependentBundleWeights.Select(w =>
                new KeyValuePair<double, double>(w.Key, bundleSteerFactor * w.Value)));
        // Initiate time-independent order poisson generator
        var orderRate = PoissonGenerator.TranslateIntoRateParameter(
            TimeSpan.FromHours(1),
            config.InventoryConfiguration.PoissonInventoryConfiguration.AverageOrdersPerHourAndStation * oStationCount);
        var TimeIndependentOrderPoissonGenerator = new PoissonGenerator(randomizer, orderRate);
        // Initiate time-independent bundle poisson generator
        var bundleRate = PoissonGenerator.TranslateIntoRateParameter(
            TimeSpan.FromHours(1),
            config.InventoryConfiguration.PoissonInventoryConfiguration.AverageBundlesPerHourAndStation * iStationCount);
        var TimeIndependentBundlePoissonGenerator = new PoissonGenerator(randomizer, bundleRate);

        // --> Test
        var simulationHours = 2 * 24;
        double simulationTime = simulationHours * 60 * 60;
        double currentTime = 0;
        var timeDependentBundleSteps = new List<double>();
        while (currentTime < simulationTime)
        {
            currentTime += TimeDependentBundlePoissonGenerator.Next(currentTime);
            timeDependentBundleSteps.Add(currentTime);
        }
        currentTime = 0;
        var timeDependentOrderSteps = new List<double>();
        while (currentTime < simulationTime)
        {
            currentTime += TimeDependentOrderPoissonGenerator.Next(currentTime);
            timeDependentOrderSteps.Add(currentTime);
        }
        currentTime = 0;
        var timeIndependentBundleSteps = new List<double>();
        while (currentTime < simulationTime)
        {
            currentTime += TimeIndependentBundlePoissonGenerator.Next(currentTime);
            timeIndependentBundleSteps.Add(currentTime);
        }
        currentTime = 0;
        var timeIndependentOrderSteps = new List<double>();
        while (currentTime < simulationTime)
        {
            currentTime += TimeIndependentOrderPoissonGenerator.Next(currentTime);
            timeIndependentOrderSteps.Add(currentTime);
        }

        // Output graph
        WriteHourBasedGraph(
            [timeDependentBundleSteps, timeDependentOrderSteps, timeIndependentBundleSteps, timeIndependentOrderSteps],
            [
                "Bundles (time-dependent)", "Orders (time-dependent)", "Bundles (time-independent)",
                "Orders (time-independent)"
            ],
            simulationHours);
    }
    public static void WriteHourBasedGraph(List<List<double>> data, List<string> captions, int overallHours)
    {
        for (var i = 0; i < data.Count; i++)
        {
            using (var sw = new StreamWriter(captions[i] + "poisson.dat"))
            {
                var hour = 1;
                while (hour <= overallHours)
                {
                    var hourSteps = data[i].TakeWhile(s => s <= TimeSpan.FromHours(hour).TotalSeconds).ToList();
                    sw.WriteLine(hour + " " + hourSteps.Count);
                    data[i].RemoveRange(0, hourSteps.Count);
                    hour++;
                }
            }
        }

        // Generate scripts to plot the data
        using (var sw = new StreamWriter("poisson.gp"))
        {
            sw.WriteLine("reset");
            sw.WriteLine("# Output definition");
            sw.WriteLine("set terminal postscript clip color eps \"Arial\" 14");
            sw.WriteLine("# Parameters");
            sw.WriteLine("set key left top Left");
            sw.WriteLine("set xlabel \"Time(h)\"");
            sw.WriteLine("set ylabel \"Count(#/h)\"");
            sw.WriteLine("set grid");
            sw.WriteLine("set style fill solid 0.25");
            sw.WriteLine("# Line-Styles");
            sw.WriteLine("set style line 1 linetype 1 linecolor rgb \"#474749\" linewidth 3");
            sw.WriteLine("set style line 2 linetype 1 linecolor rgb \"#7090c8\" linewidth 3");
            sw.WriteLine("set style line 3 linetype 1 linecolor rgb \"#42b449\" linewidth 3");
            sw.WriteLine("set style line 4 linetype 1 linecolor rgb \"#f7cb38\" linewidth 3");
            sw.WriteLine("set style line 5 linetype 1 linecolor rgb \"#db4a37\" linewidth 3");
            sw.WriteLine("set title \"Poisson - Test\"");
            sw.WriteLine("set output \"poisson.eps\"");
            sw.WriteLine("plot \\");
            for (var i = 0; i < captions.Count; i++)
            {
                if (i < captions.Count - 1)
                    sw.WriteLine("\"" + captions[i] + "poisson.dat\" u 1:2 w lines linestyle " + (i % 5 + 1) + " t \"" + captions[i] + "\", \\");
                else
                    sw.WriteLine("\"" + captions[i] + "poisson.dat\" u 1:2 w lines linestyle " + (i % 5 + 1) + " t \"" + captions[i] + "\"");
            }
            sw.WriteLine("reset");
            sw.WriteLine("exit");
        }
        using (var sw = new StreamWriter("poisson.cmd"))
        {
            sw.WriteLine("gnuplot poisson.gp");
        }
    }

    #endregion

    #region Normal distribution tests

    public static void TestGenerateNormalDistribution()
    {
        // Prepare
        var randomizer = new RandomizerSimple(0);
        var randomNumberCount = 5000;

        // --> Double related
        var randomNumbers = new List<List<double>>();
        var randomNumbersRounded = new List<List<int>>();
        var meanStdTuples = new List<Tuple<double, double, double, double>>
        {
            new(1, 2, double.NegativeInfinity, double.PositiveInfinity),
            new(10, 0.5, double.NegativeInfinity, double.PositiveInfinity),
            new(-7, 5, double.NegativeInfinity, double.PositiveInfinity),
            new(15, 5, 14.5, double.PositiveInfinity),
            new(30, 2, 29, 32),
        };
        // Draw random double numbers
        for (var j = 0; j < meanStdTuples.Count; j++)
        {
            randomNumbers.Add([]);
            for (var i = 0; i < randomNumberCount; i++)
            {
                randomNumbers[j].Add(randomizer.NextNormalDouble(meanStdTuples[j].Item1, meanStdTuples[j].Item2, meanStdTuples[j].Item3, meanStdTuples[j].Item4));
            }
        }

        // Round them to get ints
        for (var j = 0; j < meanStdTuples.Count; j++)
        {
            randomNumbersRounded.Add([]);
            for (var i = 0; i < randomNumbers[j].Count; i++)
            {
                randomNumbersRounded[j].Add((int)Math.Round(randomNumbers[j][i]));
            }
        }

        // Write them
        var fileNameBase = "normaldistribution";
        for (var i = 0; i < meanStdTuples.Count; i++)
        {
            using (var sw = new StreamWriter(fileNameBase + i + ".dat"))
            {
                foreach (var numberGroup in randomNumbersRounded[i].GroupBy(e => e).OrderBy(g => g.Key))
                {
                    sw.WriteLine(numberGroup.Key + " " + numberGroup.Count());
                }
            }
        }

        // Write plot script
        using (var sw = new StreamWriter(fileNameBase + ".gp"))
        {
            sw.WriteLine("reset");
            sw.WriteLine("# Output definition");
            sw.WriteLine("set terminal postscript clip color eps \"Arial\" 14");
            sw.WriteLine("# Parameters");
            sw.WriteLine("set key left top Left");
            sw.WriteLine("set xlabel \"Value\"");
            sw.WriteLine("set ylabel \"Count\"");
            sw.WriteLine("set grid");
            sw.WriteLine("set style fill solid 0.25");
            sw.WriteLine("# Line-Styles");
            sw.WriteLine("set style line 1 linetype 1 linecolor rgb \"#474749\" linewidth 1");
            sw.WriteLine("set style line 2 linetype 1 linecolor rgb \"#7090c8\" linewidth 1");
            sw.WriteLine("set style line 3 linetype 1 linecolor rgb \"#42b449\" linewidth 1");
            sw.WriteLine("set style line 4 linetype 1 linecolor rgb \"#f7cb38\" linewidth 1");
            sw.WriteLine("set style line 5 linetype 1 linecolor rgb \"#db4a37\" linewidth 1");
            sw.WriteLine("set title \"Normal distribution - Test\"");
            sw.WriteLine("set output \"" + fileNameBase + ".eps\"");
            sw.WriteLine("plot \\");
            for (var i = 0; i < meanStdTuples.Count; i++)
            {
                if (i < meanStdTuples.Count - 1)
                    sw.WriteLine("\"" + fileNameBase + i + ".dat\" u 1:2 w boxes linestyle " + (i % 5 + 1) +
                                 " t \"mean: " + meanStdTuples[i].Item1.ToString(IOConstants.FORMATTER) +
                                 " std: " + meanStdTuples[i].Item2.ToString(IOConstants.FORMATTER) +
                                 " lb: " + (double.IsNegativeInfinity(meanStdTuples[i].Item3) ? "na" : meanStdTuples[i].Item3.ToString(IOConstants.FORMATTER)) +
                                 " ub: " + (double.IsPositiveInfinity(meanStdTuples[i].Item4) ? "na" : meanStdTuples[i].Item4.ToString(IOConstants.FORMATTER)) + "\", \\");
                else
                    sw.WriteLine("\"" + fileNameBase + i + ".dat\" u 1:2 w boxes linestyle " + (i % 5 + 1) +
                                 " t \"mean: " + meanStdTuples[i].Item1.ToString(IOConstants.FORMATTER) +
                                 " std: " + meanStdTuples[i].Item2.ToString(IOConstants.FORMATTER) +
                                 " lb: " + (double.IsNegativeInfinity(meanStdTuples[i].Item3) ? "na" : meanStdTuples[i].Item3.ToString(IOConstants.FORMATTER)) +
                                 " ub: " + (double.IsPositiveInfinity(meanStdTuples[i].Item4) ? "na" : meanStdTuples[i].Item4.ToString(IOConstants.FORMATTER)) + "\"");
            }
            sw.WriteLine("reset");
            sw.WriteLine("exit");
        }
        using (var sw = new StreamWriter(fileNameBase + ".cmd"))
        {
            sw.WriteLine("gnuplot " + fileNameBase + ".gp");
        }

        // --> Int related
        var randomNumberCountInt = 200;
        var randomNumbersInt = new List<List<int>>();
        var meanStdTuplesInt = new List<Tuple<double, double, int, int>>
        {
            new(1, 1.5, 1, 4),
            new(4, 0.5, 0, int.MaxValue),
            new(-7, 3, int.MinValue, int.MaxValue),
        };

        // Draw random int numbers
        for (var j = 0; j < meanStdTuplesInt.Count; j++)
        {
            randomNumbersInt.Add([]);
            for (var i = 0; i < randomNumberCountInt; i++)
            {
                randomNumbersInt[j].Add(randomizer.NextNormalInt(meanStdTuplesInt[j].Item1, meanStdTuplesInt[j].Item2, meanStdTuplesInt[j].Item3, meanStdTuplesInt[j].Item4));
            }
        }

        // Write them
        var fileNameBaseInt = "normaldistributionint";
        for (var i = 0; i < meanStdTuplesInt.Count; i++)
        {
            using (var sw = new StreamWriter(fileNameBaseInt + i + ".dat"))
            {
                foreach (var numberGroup in randomNumbersInt[i].GroupBy(e => e).OrderBy(g => g.Key))
                {
                    sw.WriteLine(numberGroup.Key + " " + numberGroup.Count());
                }
            }
        }

        // Write plot script
        using (var sw = new StreamWriter(fileNameBaseInt + ".gp"))
        {
            sw.WriteLine("reset");
            sw.WriteLine("# Output definition");
            sw.WriteLine("set terminal postscript clip color eps \"Arial\" 14");
            sw.WriteLine("# Parameters");
            sw.WriteLine("set key left top Left");
            sw.WriteLine("set xlabel \"Value\"");
            sw.WriteLine("set ylabel \"Count\"");
            sw.WriteLine("set grid");
            sw.WriteLine("set style fill solid 0.25");
            sw.WriteLine("# Line-Styles");
            sw.WriteLine("set style line 1 linetype 1 linecolor rgb \"#474749\" linewidth 1");
            sw.WriteLine("set style line 2 linetype 1 linecolor rgb \"#7090c8\" linewidth 1");
            sw.WriteLine("set style line 3 linetype 1 linecolor rgb \"#42b449\" linewidth 1");
            sw.WriteLine("set style line 4 linetype 1 linecolor rgb \"#f7cb38\" linewidth 1");
            sw.WriteLine("set style line 5 linetype 1 linecolor rgb \"#db4a37\" linewidth 1");
            sw.WriteLine("set title \"Normal distribution (int) - Test\"");
            sw.WriteLine("set output \"" + fileNameBaseInt + ".eps\"");
            sw.WriteLine("plot \\");
            for (var i = 0; i < meanStdTuplesInt.Count; i++)
            {
                if (i < meanStdTuplesInt.Count - 1)
                    sw.WriteLine("\"" + fileNameBaseInt + i + ".dat\" u 1:2 w boxes linestyle " + (i % 5 + 1) +
                                 " t \"mean: " + meanStdTuplesInt[i].Item1.ToString(IOConstants.FORMATTER) +
                                 " std: " + meanStdTuplesInt[i].Item2.ToString(IOConstants.FORMATTER) +
                                 " lb: " + (int.MinValue == meanStdTuplesInt[i].Item3 ? "na" : meanStdTuplesInt[i].Item3.ToString(IOConstants.FORMATTER)) +
                                 " ub: " + (int.MaxValue == meanStdTuplesInt[i].Item4 ? "na" : meanStdTuplesInt[i].Item4.ToString(IOConstants.FORMATTER)) + "\", \\");
                else
                    sw.WriteLine("\"" + fileNameBaseInt + i + ".dat\" u 1:2 w boxes linestyle " + (i % 5 + 1) +
                                 " t \"mean: " + meanStdTuplesInt[i].Item1.ToString(IOConstants.FORMATTER) +
                                 " std: " + meanStdTuplesInt[i].Item2.ToString(IOConstants.FORMATTER) +
                                 " lb: " + (int.MinValue == meanStdTuplesInt[i].Item3 ? "na" : meanStdTuplesInt[i].Item3.ToString(IOConstants.FORMATTER)) +
                                 " ub: " + (int.MaxValue == meanStdTuplesInt[i].Item4 ? "na" : meanStdTuplesInt[i].Item4.ToString(IOConstants.FORMATTER)) + "\"");
            }
            sw.WriteLine("reset");
            sw.WriteLine("exit");
        }
        using (var sw = new StreamWriter(fileNameBaseInt + ".cmd"))
        {
            sw.WriteLine("gnuplot " + fileNameBaseInt + ".gp");
        }
    }

    #endregion
}