# HandyTerminal

This repository contains `HandyTerminal`, a simple Avalonia-based terminal command manager.

## Where to find the program

After publishing for Linux, the executable is located in:

- `publish/linux-x64/HandyTerminal`

That is the file you run on Ubuntu or other Linux x86_64 systems.

## How to run it on Linux

1. Extract or copy the `publish/linux-x64` folder to your Linux machine.
2. Open a terminal in that folder.
3. Make the executable runnable:

   ```bash
   chmod +x HandyTerminal
   ```

4. Run the app:

   ```bash
   ./HandyTerminal
   ```

If you want a single portable AppImage instead, use the included `build-appimage.sh` script after publishing.

## How to run from source on Windows

1. Open the repository folder in a terminal.
2. Run:

   ```powershell
   dotnet run
   ```

The application will launch from the source project.

## Notes

- The published Linux binary is the one command-line executable file named `HandyTerminal`.
- If you want to share the app, copy the `publish/linux-x64` folder or the generated AppImage file.
