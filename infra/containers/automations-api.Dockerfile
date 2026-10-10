FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/global.json src/Directory.Build.props ./src/
COPY src/services/automations ./src/services/automations
COPY docs/schemas ./docs/schemas
RUN cd src && dotnet restore services/automations/Automations.Api.csproj --locked-mode
RUN cd src && dotnet publish services/automations/Automations.Api.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
RUN mkdir -p /data /keys && chown app:app /data /keys
USER app
ENV ASPNETCORE_URLS=http://+:4310 AUTOMATIONS_DATA_DIR=/data
EXPOSE 4310
ENTRYPOINT ["dotnet", "Automations.Api.dll"]
