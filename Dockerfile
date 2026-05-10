FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY EgresosBot.sln ./
COPY src/EgresosBot.Api/EgresosBot.Api.csproj src/EgresosBot.Api/
RUN dotnet restore EgresosBot.sln

COPY . .
RUN dotnet publish src/EgresosBot.Api/EgresosBot.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish .

EXPOSE 8080

ENTRYPOINT ["dotnet", "EgresosBot.Api.dll"]
