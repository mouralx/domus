FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY src/global.json src/Directory.Build.props ./src/
COPY src/services/boards ./src/services/boards
COPY src/services/membership ./src/services/membership
RUN cd src && dotnet restore services/membership/Domus.Membership.Api.csproj --locked-mode
RUN cd src && dotnet publish services/membership/Domus.Membership.Api.csproj -c Release --no-restore -o /out
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out ./
ENV ASPNETCORE_URLS=http://+:5190
ENTRYPOINT ["dotnet", "Domus.Membership.Api.dll"]
