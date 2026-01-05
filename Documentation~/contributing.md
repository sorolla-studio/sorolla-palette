# Contributing to Sorolla SDK

Thank you for your interest in contributing to Sorolla SDK!

---

## Quick Start for Contributors

### Prerequisites

- Unity 2022.3 LTS or later
- Git
- macOS (for iOS testing) or Windows/Linux (Android only)

### Setup

1. Clone the repository:
   ```bash
   git clone https://github.com/LaCreArthur/sorolla-palette-upm.git
   ```

2. Open in Unity as a local package or link via `manifest.json`:
   ```json
   {
     "dependencies": {
       "com.sorolla.sdk": "file:../sorolla-palette-upm"
     }
   }
   ```

3. Import the Debug UI sample for testing

---

## Project Structure

```
Runtime/
├── SorollaSDK.cs              ← Main public API
├── SorollaBootstrapper.cs     ← Auto-initialization
├── SorollaConfig.cs           ← Configuration asset
└── Adapters/                  ← Third-party SDK wrappers

Editor/
├── SorollaWindow.cs           ← Configuration UI
├── SorollaSettings.cs         ← Mode management
└── Sdk/                       ← SDK detection/installation

Samples~/DebugUI/              ← Debug panel sample
```

---

## Development Guidelines

### Code Style

- Follow C# naming conventions
- Use XML documentation for public APIs
- Keep adapter classes static with conditional compilation
- Use `#if SDK_DEFINE` guards for optional SDKs

### Adding a New SDK Adapter

1. Add SDK metadata to `Editor/Sdk/SdkRegistry.cs`
2. Create adapter in `Runtime/Adapters/NewSdkAdapter.cs`
3. Add scripting define in `Editor/Sdk/DefineSymbols.cs`
4. Initialize in `SorollaSDK.Initialize()`
5. Add UI section in `Editor/SorollaWindow.cs` if needed

### Testing

1. Test in Editor with fake dialogs
2. Test on iOS device (ATT flow)
3. Test on Android device
4. Use Debug UI to verify integration

---

## Pull Request Process

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/my-feature`
3. Make your changes
4. Update documentation if needed
5. Update CHANGELOG.md
6. Submit PR with clear description

### PR Checklist

- [ ] Code follows project style
- [ ] Public APIs documented
- [ ] No compiler warnings
- [ ] Tested on target platforms
- [ ] CHANGELOG.md updated

---

## Reporting Issues

Please include:
- Unity version
- SDK version
- Platform (iOS/Android/Editor)
- Steps to reproduce
- Full error message
- Debug UI screenshots if relevant

---

## Questions?

- 💬 [GitHub Issues](https://github.com/LaCreArthur/sorolla-palette-upm/issues)
- 💬 [GitHub Discussions](https://github.com/LaCreArthur/sorolla-palette-upm/discussions)
- 📖 [Prototype Setup Guide](prototype-setup.md)
- 📖 [Full Mode Setup Guide](full-setup.md)
