using RAWSimO.Core.Control;
using RAWSimO.WebServer.Shared.SimulationHost.Services;
using RAWSimO.WebServer.SimulationHost.Services;

namespace RAWSimO.WebServer.SimulationHost;

public static class SimulationHostDependencyInjection
{
	public static void AddSimulationHostModule(this IServiceCollection self)
	{
		self.AddSingleton<SimulationHostServices>();
		self.AddSingleton<ISimulationHostService>(sp => sp.GetRequiredService<SimulationHostServices>());
		self.AddSingleton<ISimulationStreamService>(sp => sp.GetRequiredService<SimulationHostServices>());
		self.AddSingleton<IInstanceProvider>(sp => sp.GetRequiredService<SimulationHostServices>());
		// Simulation data-source backend switch (internal RAWSim-O vs external/Isaac physical).
		// Backs the process-global SimBackendOptions; settable via the SetSimBackend endpoint.
		self.AddSingleton<ISimBackendOptions, SimBackendOptionsAccessor>();
	}

	public static void UseSimulationHostModule(this WebApplication self)
	{
		self.Services.GetRequiredService<SimulationHostServices>();
	}
}