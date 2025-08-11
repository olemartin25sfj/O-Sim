FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Kopier csproj først for lag cache
COPY AlarmService/AlarmService.csproj AlarmService/
COPY ../OSim.Shared.Messages/OSim.Shared.Messages.csproj OSim.Shared.Messages/

RUN dotnet restore AlarmService/AlarmService.csproj

# Kopier resten av koden
COPY . .

RUN dotnet publish AlarmService/AlarmService.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/runtime:8.0
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "AlarmService.dll"]
