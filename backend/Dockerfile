# syntax=docker/dockerfile:1
# Build context = thu muc "backend/" (noi chua GymMaster.API/)

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Restore rieng de tan dung cache layer
COPY GymMaster.API/GymMaster.API.csproj GymMaster.API/
RUN dotnet restore GymMaster.API/GymMaster.API.csproj

# Copy toan bo source roi publish
COPY GymMaster.API/ GymMaster.API/
RUN dotnet publish GymMaster.API/GymMaster.API.csproj \
    -c Release -o /app/publish /p:UseAppHost=false

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Cloud Run gui request vao $PORT (mac dinh 8080)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "GymMaster.API.dll"]
