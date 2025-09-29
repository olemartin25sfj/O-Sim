FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for caching (paths are relative to the build context ./src)
COPY AlarmService/AlarmService.csproj AlarmService/
COPY OSim.Shared.Messages/OSim.Shared.Messages.csproj OSim.Shared.Messages/

# Restore dependencies for the specific project
RUN dotnet restore AlarmService/AlarmService.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish AlarmService/AlarmService.csproj -c Release -o /app/publish --no-restore

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "AlarmService.dll"]
