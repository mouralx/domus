FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/global.json src/Directory.Build.props ./src/
COPY src/services/boards ./src/services/boards
COPY src/workers/boards ./src/workers/boards
RUN cd src && dotnet restore workers/boards/Kanbada.Worker.csproj --locked-mode
RUN cd src && dotnet publish workers/boards/Kanbada.Worker.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
ENTRYPOINT ["dotnet", "Kanbada.Worker.dll"]
