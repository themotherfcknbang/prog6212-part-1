# syntax=docker/dockerfile:1
# Build context is the SOLUTION folder (where RaceDay.sln lives):
#   docker build -f RaceDay.Api/Dockerfile -t raceday-api .

# ---- runtime base: small ASP.NET image, runs as a non-root user ----
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
USER app
WORKDIR /app
EXPOSE 8080

# ---- build: full SDK, thrown away after publishing ----
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
# Copy only the project file first, so the (slow) restore layer is cached until packages change
COPY ["RaceDay.Api/RaceDay.Api.csproj", "RaceDay.Api/"]
RUN dotnet restore "RaceDay.Api/RaceDay.Api.csproj"
COPY RaceDay.Api/ RaceDay.Api/
RUN dotnet publish "RaceDay.Api/RaceDay.Api.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# ---- final: runtime image + published output only ----
FROM base AS final
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "RaceDay.Api.dll"]
