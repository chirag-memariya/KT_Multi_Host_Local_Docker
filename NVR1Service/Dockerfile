# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Only copy the .csproj file (since context is root, and the .csproj is in NVR1Service/)
COPY NVR1Service/NVR1Service.csproj NVR1Service/
RUN dotnet restore "NVR1Service/NVR1Service.csproj"

# Copy everything else
COPY NVR1Service/ NVR1Service/
WORKDIR /src/NVR1Service
RUN dotnet publish "NVR1Service.csproj" -c Release -o /app/publish

# Stage 2: Create the final runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 8080
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "NVR1Service.dll"]
