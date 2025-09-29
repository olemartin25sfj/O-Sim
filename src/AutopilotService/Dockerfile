FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution/project files for cache
COPY OSim.sln ./
COPY src/AutopilotService/AutopilotService.csproj src/AutopilotService/
COPY src/OSim.Shared.Messages/OSim.Shared.Messages.csproj src/OSim.Shared.Messages/

RUN dotnet restore

COPY . .
RUN dotnet publish src/AutopilotService/AutopilotService.csproj -c Release -o /app/publish --no-restore

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AutopilotService.dll"]
