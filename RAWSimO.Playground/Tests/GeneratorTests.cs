using RAWSimO.Core.Configurations;
using RAWSimO.Core.Generator;
using RAWSimO.Core.IO;

namespace RAWSimO.Playground.Tests;

public class GeneratorTests
{
    public static void TestGenerateDefaultSimpleItemConfig()
    {
        var config = OrderGenerator.GenerateSimpleItemConfiguration(new OrderGenerator.SimpleItemGeneratorPreConfiguration
        {
            ItemDescriptionCount = 100,
            DefaultWeight = 1,
            DefaultCoWeight = 1,
            ProbabilityWeightNormalMu = 1,
            ProbabilityWeightNormalSigma = 3,
            ItemWeightLB = 1,
            ItemWeightUB = 7,
            ItemWeightMu = 2,
            ItemWeightSigma = 1,
            GivenCoWeights = 1
        });
        InstanceIO.WriteSimpleItemGeneratorConfigFile("simpleitemgeneratorconfig.xml", config);
    }
}