FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/global.json src/Directory.Build.props ./src/
COPY src/services/automations/Core ./src/services/automations/Core
COPY src/workers/automations ./src/workers/automations
COPY docs/schemas ./docs/schemas
RUN cd src && dotnet restore workers/automations/Automations.Worker.csproj --locked-mode
RUN cd src && dotnet publish workers/automations/Automations.Worker.csproj -c Release --no-restore -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
RUN mkdir -p /data && chown app:app /data
USER app
ENV ASPNETCORE_URLS=http://+:4320 AUTOMATIONS_DATA_DIR=/data
EXPOSE 4320
ENTRYPOINT ["dotnet", "Automations.Worker.dll"]
