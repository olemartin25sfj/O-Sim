FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files first for better caching
COPY OSim.sln ./
COPY src/AlarmService/AlarmService.csproj src/AlarmService/
COPY src/OSim.Shared.Messages/OSim.Shared.Messages.csproj src/OSim.Shared.Messages/

# Restore dependencies
RUN dotnet restore

# Copy everything and publish
COPY . .
RUN dotnet publish src/AlarmService/AlarmService.csproj -c Release -o /app/publish --no-restore

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "AlarmService.dll"]
