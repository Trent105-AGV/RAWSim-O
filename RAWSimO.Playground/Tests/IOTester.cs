using RAWSimO.Core;
using RAWSimO.Core.Control;

namespace RAWSimO.Playground.Tests
{
    public class IOTester
    {
        public static void ExecuteInstance(Instance instance)
        {
            // Deus ex machina
            SimulationExecutor.Execute(instance);
        }
    }
}
