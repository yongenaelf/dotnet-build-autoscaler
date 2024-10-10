# Use the official ASP.NET Core runtime image as a base for .NET 8
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Use the official .NET SDK image to build the application for .NET 8
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy the .csproj files and restore dependencies for the application
COPY ["BuildJobApi.csproj", ""]
RUN dotnet restore "BuildJobApi.csproj"

# Copy the whole project source code and build it
COPY . ./
RUN dotnet build -c Release -o /app/build

# Publish the application to a folder for the app
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish

# Build the final runtime image
FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .

# Set the entry point command for the container startup
ENTRYPOINT ["dotnet", "BuildJobApi.dll"]