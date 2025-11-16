# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore as distinct layers
COPY ["TheRockWasteManagement.csproj", "."]
RUN dotnet restore "TheRockWasteManagement.csproj"

# Copy everything else and build
COPY . .
WORKDIR "/src"
RUN dotnet build "TheRockWasteManagement.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "TheRockWasteManagement.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Install dependencies for Firebase and other libraries
RUN apt-get update && apt-get install -y \
    libc6-dev \
    libgdiplus \
    && rm -rf /var/lib/apt/lists/*

# Set environment variables for Cloud Run
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "TheRockWasteManagement.dll"]