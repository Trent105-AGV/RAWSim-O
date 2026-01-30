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
	}

	public static void UseSimulationHostModule(this WebApplication self)
	{
		self.Services.GetRequiredService<SimulationHostServices>();
	}
}