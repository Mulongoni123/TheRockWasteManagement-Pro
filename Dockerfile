# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project file and restore
COPY *.csproj .
RUN dotnet restore

# Copy everything and build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Use PORT environment variable (for Cloud Run)
ENV ASPNETCORE_URLS=http://+:8080
ENV PORT=8080
EXPOSE 8080

# Use a wildcard to find the DLL automatically
ENTRYPOINT ["dotnet", "TheRockWasteManagement.dll"]