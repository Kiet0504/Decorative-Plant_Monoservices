# See https://aka.ms/customizecontainer to learn how to customize your debug container and how Visual Studio uses this Dockerfile to build your images for faster debugging.

# This stage is used when running from VS in fast mode (Default for Debug configuration)
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS base
USER $APP_UID
WORKDIR /app
EXPOSE 8080
EXPOSE 8081


# This stage is used to build the service project
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG BUILD_CONFIGURATION=Release
WORKDIR /src
COPY ["DecorativePlant-BE.sln", "."]
COPY ["decorativeplant-be.API/decorativeplant-be.API.csproj", "decorativeplant-be.API/"]
COPY ["decorativeplant-be.Domain/decorativeplant-be.Domain.csproj", "decorativeplant-be.Domain/"]
COPY ["decorativeplant-be.Application/decorativeplant-be.Application.csproj", "decorativeplant-be.Application/"]
COPY ["decorativeplant-be.Infrastructure/decorativeplant-be.Infrastructure.csproj", "decorativeplant-be.Infrastructure/"]
RUN dotnet restore "DecorativePlant-BE.sln"
COPY . .
WORKDIR "/src/decorativeplant-be.API"
RUN dotnet build "./decorativeplant-be.API.csproj" -c $BUILD_CONFIGURATION -o /app/build

# This stage is used to publish the service project to be copied to the final stage
FROM build AS publish
ARG BUILD_CONFIGURATION=Release
RUN dotnet publish "./decorativeplant-be.API.csproj" -c $BUILD_CONFIGURATION -o /app/publish /p:UseAppHost=false

# This stage is used in production or when running from VS in regular mode (Default when not using the Debug configuration)
FROM base AS final
# Install gosu to fix Data Protection volume permissions (run chown as root, then exec as app user)
USER root
RUN apt-get update && apt-get install -y --no-install-recommends gosu \
    && rm -rf /var/lib/apt/lists/*
COPY decorativeplant-be.API/entrypoint.sh /entrypoint.sh
RUN chmod +x /entrypoint.sh
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["/entrypoint.sh"]
