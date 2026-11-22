# Stage 1: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/InternTrack.Domain/InternTrack.Domain.csproj", "InternTrack.Domain/"]
COPY ["src/InternTrack.Application/InternTrack.Application.csproj", "InternTrack.Application/"]
COPY ["src/InternTrack.Infrastructure/InternTrack.Infrastructure.csproj", "InternTrack.Infrastructure/"]
COPY ["src/InternTrack.Api/InternTrack.Api.csproj", "InternTrack.Api/"]

RUN dotnet restore "InternTrack.Api/InternTrack.Api.csproj"

# Copy all source files and publish
COPY src/ .
WORKDIR "/src/InternTrack.Api"
RUN dotnet publish "InternTrack.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Polices pour QuestPDF, curl pour le healthcheck, tzdata pour le fuseau Africa/Casablanca
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    fontconfig \
    fonts-liberation \
    fonts-dejavu-core \
    tzdata \
    && rm -rf /var/lib/apt/lists/*

# Fichiers déposés (mémoires, conventions signées) et clés de protection des données : volume persistant.
RUN mkdir -p /app/data/uploads /app/data/keys
VOLUME ["/app/data"]

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "InternTrack.Api.dll"]
