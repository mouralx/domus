FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/global.json src/Directory.Build.props ./src/
COPY src/services/mcp ./src/services/mcp
RUN cd src && dotnet restore services/mcp/Domus.Mcp.Server.csproj --locked-mode
RUN cd src && dotnet publish services/mcp/Domus.Mcp.Server.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
USER app
ENV ASPNETCORE_URLS=http://+:4340
EXPOSE 4340
ENTRYPOINT ["dotnet", "Domus.Mcp.Server.dll"]
