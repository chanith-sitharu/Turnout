# =============================================================
# Stage 1: Build & Publish Application
# =============================================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj and restore package dependencies
COPY ["TurnoutStore.csproj", "./"]
RUN dotnet restore "TurnoutStore.csproj"

# Copy the rest of the application files
COPY . .

# Build and publish in Release mode
RUN dotnet publish "TurnoutStore.csproj" -c Release -o /app/publish /p:UseAppHost=false

# =============================================================
# Stage 2: Production Runtime
# =============================================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Ensure runtime directories exist for user file uploads
RUN mkdir -p /app/wwwroot/uploads/slips /app/wwwroot/images/products

# Copy compiled files from build stage
COPY --from=build /app/publish .

# Expose standard ASP.NET Core 8 container port
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TurnoutStore.dll"]
