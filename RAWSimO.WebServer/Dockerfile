# syntax=docker.io/docker/dockerfile:1.7-labs
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
ENV DOTNET_EnableDynamicCode=true

# Layer 1: Copy all dependencies (shared libraries, rarely change)
# This layer will be cached unless dependencies are updated
COPY --exclude=RAWSimO.Core.dll \
     --exclude=RAWSimO.Core.pdb \
     --exclude=RAWSimO.WebServer.pdb \
     --exclude=RAWSimO.WebServer.dll \
     --exclude=RAWSimO.WebServer.Shared.pdb \
     --exclude=RAWSimO.WebServer.Shared.dll \
     --exclude=RAWSimO.Rendering2D.pdb \
     --exclude=RAWSimO.Rendering2D.dll \
     --exclude=RAWSimO.MultiAgentPathFinding.pdb \
     --exclude=RAWSimO.MultiAgentPathFinding.dll \
     --exclude=RAWSimO.Toolbox.pdb \
     --exclude=RAWSimO.Toolbox.dll \
     --exclude=RAWSimO.WebServer.deps.json \
     --exclude=RAWSimO.WebServer.runtimeconfig.json \
     --exclude=RAWSimO.WebServer \
     --exclude=RAWSimO.Core.xml \
     --exclude=appsettings*.json \
     --exclude=web.config \
     . .

# Layer 2: Copy application-specific files (change frequently)
# This layer rebuilds when application code changes
COPY RAWSimO.Core.dll \
     RAWSimO.Core.pdb \
     RAWSimO.WebServer.dll \
     RAWSimO.WebServer.pdb \
     RAWSimO.WebServer.Shared.dll \
     RAWSimO.WebServer.Shared.pdb \
     RAWSimO.Rendering2D.dll \
     RAWSimO.Rendering2D.pdb \
     RAWSimO.MultiAgentPathFinding.dll \
     RAWSimO.MultiAgentPathFinding.pdb \
     RAWSimO.Toolbox.dll \
     RAWSimO.Toolbox.pdb \
     RAWSimO.WebServer.deps.json \
     RAWSimO.WebServer.runtimeconfig.json \
     RAWSimO.WebServer \
     RAWSimO.Core.xml \
     appsettings*.json \
     web.config \
     ./

ENTRYPOINT ["dotnet", "RAWSimO.WebServer.dll"]
