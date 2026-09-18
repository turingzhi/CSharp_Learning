FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY src/IssueTracker.Core/IssueTracker.Core.csproj src/IssueTracker.Core/
COPY src/IssueTracker.Api/IssueTracker.Api.csproj src/IssueTracker.Api/
RUN dotnet restore src/IssueTracker.Api/IssueTracker.Api.csproj

COPY src/ src/
RUN dotnet publish src/IssueTracker.Api/IssueTracker.Api.csproj \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "IssueTracker.Api.dll"]