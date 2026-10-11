FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/global.json src/Directory.Build.props ./src/
COPY src/services/boards ./src/services/boards
RUN cd src && dotnet restore services/boards/Domus.Boards.Api.csproj --locked-mode
RUN cd src && dotnet publish services/boards/Domus.Boards.Api.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
COPY src/services/boards/docker-entrypoint.sh /usr/local/bin/boards-entrypoint
ENV ASPNETCORE_URLS=http://+:5180
ENTRYPOINT ["/bin/sh", "/usr/local/bin/boards-entrypoint"]
CMD ["dotnet", "Domus.Boards.Api.dll"]
