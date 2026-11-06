# Multi-stage Dockerfile for BarberBook ASP.NET Core 8 MVC Web Application

# Stage 1: Build & Publish
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project definitions for optimized caching
COPY ["BarberBook.sln", "./"]
COPY ["src/BarberBook.Data/BarberBook.Data.csproj", "src/BarberBook.Data/"]
COPY ["src/BarberBook.Web/BarberBook.Web.csproj", "src/BarberBook.Web/"]
COPY ["tests/BarberBook.Tests/BarberBook.Tests.csproj", "tests/BarberBook.Tests/"]

RUN dotnet restore "BarberBook.sln"

# Copy full repository source
COPY . .

# Build and Publish in Release mode
WORKDIR "/src/src/BarberBook.Web"
RUN dotnet publish "BarberBook.Web.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 2: Runtime Image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Non-root user security best practice
USER $APP_UID

# Copy compiled artifacts from build stage
COPY --from=build /app/publish .

# Environment defaults
ENV ASPNETCORE_URLS=http://+:5000 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

EXPOSE 5000

ENTRYPOINT ["dotnet", "BarberBook.Web.dll"]

