# RoslynCustomAnalyzer

A custom Roslyn analyzer for Unity Dots that provides static analysis rules to improve code correctness and performance in Unity Entities systems.

Note: This readme file and the code was partly AI generated and may contains errors.

## Features

This analyzer includes rules to enforce best practices in Unity Dots development:

- Enforce explicit read-only parameters for `SystemAPI.GetComponentLookup` calls
- Warn about modifications to read-only component copies that won't be persisted

## Installation

### Download .dll from the Release folder

1. Download latest form the Release folder (dll file only)
2. Copy the generated `RoslynCustomAnalyzer.dll` to your Unity project's `Assets/Plugins/Analyzers` folder.
3. Disable/Uncheck all under "Selected platforms for plugin" in the inspector
4. Click blue icon on the bottom and seelct RoslyanAnalizer
5. Restart Visual Studio or your IDE to load the analyzer.


### Manual Installation

1. Build the analyzer project in Release mode:
   ```bash
   dotnet build --configuration Release
   ```

2. Copy the generated `RoslynCustomAnalyzer.dll` from the `bin/Release/netstandard2.0` directory to your Unity project's `Assets/Plugins/Analyzers` folder.

3. Restart Visual Studio or your IDE to load the analyzer.

## Usage

Once installed, the analyzer will automatically run during compilation and show warnings in your IDE. The rules are enabled by default.

You can configure the analyzer rules in your `.editorconfig` file:

```ini
[*.cs]
dotnet_diagnostic.UnityRedDots001.severity = warning
dotnet_diagnostic.UnityRedDots002.severity = warning
```

## Custom Warnings

| ID | Title | Description | Severity |
|----|-------|-------------|----------|
| UnityRedDots001 | Modification of read-only component copy | Variable '{0}' is a copy from a [ReadOnly] source and will not be saved | Warning |
| UnityRedDots002 | GetComponentLookup missing read-only parameter | SystemAPI.GetComponentLookup must explicitly specify the read-only parameter (true or false) | Warning |

## Building from Source

### Build Steps

1. Clone the repository:
   ```bash
   git clone https://github.com/AiSatan/RoslynCustomAnalyzer.git
   cd RoslynCustomAnalyzer
   ```

2. Restore dependencies:
   ```bash
   dotnet restore
   ```

3. Build the solution:
   ```bash
   dotnet build --configuration Release
   ```

4. Run tests:
   ```bash
   dotnet test
   ```

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Requirements

- .NET Standard 2.0
- Unity 6 or later (for Unity Dots support)

## Support

If you encounter any issues or have questions, please open an issue on GitHub.