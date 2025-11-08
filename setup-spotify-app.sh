#!/bin/bash

# Spotify Genre Organizer Setup Script
# This script creates the solution and project structure

set -e  # Exit on error

echo "Creating Spotify Genre Organizer solution..."

# Create solution
dotnet new sln -n SpotifyGenreOrganizer

# Create console application project
dotnet new console -n SpotifyGenreOrganizer -o src/SpotifyGenreOrganizer

# Add project to solution
dotnet sln SpotifyGenreOrganizer.sln add src/SpotifyGenreOrganizer/SpotifyGenreOrganizer.csproj

# Navigate to project directory
cd src/SpotifyGenreOrganizer

# Add required NuGet packages
echo "Adding NuGet packages..."
dotnet add package SpotifyAPI.Web
dotnet add package SpotifyAPI.Web.Auth
dotnet add package Microsoft.Extensions.Configuration
dotnet add package Microsoft.Extensions.Configuration.Json
dotnet add package Microsoft.Extensions.Configuration.Binder
dotnet add package Microsoft.Extensions.Configuration.EnvironmentVariables
dotnet add package Newtonsoft.Json

# Return to root directory
cd ../..

echo "Setup complete!"
echo ""
echo "Next steps:"
echo "1. Create a .env or appsettings.json file with your Spotify credentials"
echo "2. Run 'dotnet restore' to ensure all packages are installed"
echo "3. Run 'dotnet build' to build the solution"
echo "4. Run 'dotnet run --project src/SpotifyGenreOrganizer' to start the app"
