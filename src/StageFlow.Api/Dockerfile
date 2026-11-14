# Stage 1: Build .NET API
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/StageFlow.Domain/StageFlow.Domain.csproj", "StageFlow.Domain/"]
COPY ["src/StageFlow.Application/StageFlow.Application.csproj", "StageFlow.Application/"]
COPY ["src/StageFlow.Infrastructure/StageFlow.Infrastructure.csproj", "StageFlow.Infrastructure/"]
COPY ["src/StageFlow.Api/StageFlow.Api.csproj", "StageFlow.Api/"]

RUN dotnet restore "StageFlow.Api/StageFlow.Api.csproj"

# Copy all source files and publish
COPY src/ .
WORKDIR "/src/StageFlow.Api"
RUN dotnet publish "StageFlow.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime environment
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
EXPOSE 8080
EXPOSE 8081

# Install fonts for QuestPDF PDF generation and curl for the container healthcheck
RUN apt-get update && apt-get install -y --no-install-recommends \
    curl \
    fontconfig \
    fonts-liberation \
    fonts-dejavu-core \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "StageFlow.Api.dll"]
