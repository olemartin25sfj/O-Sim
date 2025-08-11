FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Kopier hele kildekoden (bin/obj blir filtrert bort av .dockerignore)
COPY . .

# Publisering (restore kjøres automatisk som del av publish fordi vi ikke bruker --no-restore)
RUN dotnet publish AlarmService/AlarmService.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV DOTNET_EnableDiagnostics=0
ENTRYPOINT ["dotnet", "AlarmService.dll"]
