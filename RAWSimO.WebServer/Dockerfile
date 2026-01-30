# # syntax=docker.io/docker/dockerfile:1.7-labs
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
WORKDIR /app
ENV DOTNET_EnableDynamicCode=true

# Layer 1: Copy all dependencies (shared libraries, rarely change)
COPY ./dep/* .

# Layer 2: Copy application-specific files (change frequently)
COPY ./webserver/* .

ENTRYPOINT ["dotnet", "RAWSimO.WebServer.dll"]
