# FoodbookApp

FoodbookApp is a cross-platform meal and recipe planner built with .NET MAUI. It uses Entity Framework Core with a local SQLite database stored inside the app's data directory and seeds initial data from `Resources/Raw/ingredients.json`.

## Requirements

- **.NET SDK**: .NET 9 (preview). Make sure the .NET 9 preview SDK is installed.

## Setup

1. Install the MAUI workload:
   ```bash
   dotnet workload install maui
   ```
2. Restore and build the solution:
   ```bash
   dotnet build FoodbookApp.sln
   ```

## Deploying to Android

To run the Android version on an emulator (for example a Pixel 7 emulator), start the emulator with Android Studio or the `emulator` tool, then execute:

```bash
dotnet build -t:Run -f net9.0-android /p:AndroidEmulator=Pixel_7_API_33
```

Replace `Pixel_7_API_33` with the name of your emulator (use `emulator -list-avds` to list available emulators).

## Environment

No additional environment variables are required. The application stores its data in a SQLite file `foodbook.db` within the app's data directory and loads ingredient information from `Resources/Raw/ingredients.json`.

