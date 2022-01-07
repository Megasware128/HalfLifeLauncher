# Half-Life Launcher

Simple CLI tool I've created to assist me with hosting of a HL: Decay game.

## Features
1. Supports tab completion (of maps and DLC/mods) with [dotnet-suggest](https://github.com/dotnet/command-line-api/blob/main/docs/dotnet-suggest.md)
2. Is a .NET Global Tool (currently not on NuGet)

## Build
1. Make sure .NET 6.0 is installed
2. Check if paths are correct in appsettings.json
3. Run `./build.cmd install` or `./build.cmd update` if already installed
4. Optional: for tab completion install [dotnet-suggest](https://github.com/dotnet/command-line-api/blob/main/docs/dotnet-suggest.md)
