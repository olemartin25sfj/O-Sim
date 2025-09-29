FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for caching
COPY SimulatorService/SimulatorService.csproj SimulatorService/
COPY OSim.Shared.Messages/OSim.Shared.Messages.csproj OSim.Shared.Messages/

RUN dotnet restore SimulatorService/SimulatorService.csproj

COPY . .
RUN dotnet publish SimulatorService/SimulatorService.csproj -c Release -o /app/publish --no-restore

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "SimulatorService.dll"]
