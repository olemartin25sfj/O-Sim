FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files for caching
COPY LoggerService/LoggerService.csproj LoggerService/
COPY OSim.Shared.Messages/OSim.Shared.Messages.csproj OSim.Shared.Messages/

RUN dotnet restore LoggerService/LoggerService.csproj

COPY . .
RUN dotnet publish LoggerService/LoggerService.csproj -c Release -o /app/publish --no-restore

FROM runtime AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "LoggerService.dll"]
