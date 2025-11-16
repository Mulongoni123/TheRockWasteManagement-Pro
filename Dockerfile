# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore dependencies
COPY *.csproj .
RUN dotnet restore

# Copy everything else and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Install necessary packages for ASP.NET Core
RUN apt-get update && apt-get install -y \
    libc6-dev \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

# Copy built application from build stage
COPY --from=build /app/publish .

# Set the port ASP.NET Core will use
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Expose port 8080
EXPOSE 8080

# Start the application
ENTRYPOINT ["dotnet", "TheRockWasteManagement.dll"]