FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/global.json src/Directory.Build.props ./src/
COPY src/services/ai ./src/services/ai
RUN cd src && dotnet restore services/ai/Domus.Ai.Api.csproj --locked-mode
RUN cd src && dotnet publish services/ai/Domus.Ai.Api.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /out ./
RUN mkdir -p /data/keys && chown -R app:app /data && chmod 700 /data /data/keys
USER app
ENV ASPNETCORE_URLS=http://+:4330 AI_DATA_DIR=/data
EXPOSE 4330
ENTRYPOINT ["dotnet", "Domus.Ai.Api.dll"]
