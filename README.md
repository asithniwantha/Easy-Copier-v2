# Easy Copier v2

A modern Windows desktop media and software distribution manager built with **WinUI 3 / Windows App SDK**, **.NET 11**, and **C# 15**, structured using **Single-Project Vertical Slice Architecture (VSA)**.

## Architecture Overview

`Easy-Copier-v2` organizes UI views, ViewModels, domain models, and feature-specific infrastructure into autonomous feature slices while keeping shared cross-cutting primitives compact and dependency-free.

```text
Easy-Copier-v2/
├── CompositionRoot.cs                  # Central DI registration across Shared + Features
├── App.xaml / App.xaml.cs              # Application lifecycle, notifications, unhandled error capture
├── Program.cs                          # Unpackaged Velopack startup entry point
├── Assets/                             # Application icons and audio notification clips
├── Shared/                             # Cross-cutting infrastructure reused by 3+ slices
│   ├── Configuration/                  # AppSettings & SettingsService (%LocalAppData%)
│   ├── Interop/                        # Win32 windowing helpers & AppWindow sizing/ownership
│   ├── Messages/                       # Immutable sealed record WeakReferenceMessenger contracts
│   ├── Models/                         # PagedResult, ValidationResult, SourceFolder
│   ├── Persistence/                    # SQLite connection factory
│   ├── Platform/                       # Dispatcher, WindowService, Dialog/Picker/Tray abstractions
│   └── UI/                             # Shared Styles.xaml, FormattingHelpers, ValueConverters
└── Features/
    ├── Shell/                          # MainWindow, MainPage, ShellViewModel, cross-tab Contracts/
    │   └── Contracts/                  # LibraryCategory, GameEntry, SelectionCoordinator, FlyoutHelper
    ├── SmartAdder/                     # Floating sum calculator & SQLite calculation history
    ├── Settings/                       # Multi-section Settings window, Velopack updater, About dialog
    ├── OsImageLauncher/                # ISO discovery, Rufus discovery/launching, OS Image flyout
    ├── TransferQueue/                  # Removable drive detection, validation, Windows Shell copy queue & verification
    ├── Library/                        # Games/Apps/Films & TV scanning, cache, genre metadata, Details & Duplicate Manager
    └── History/                        # Copy history SQLite ledger, 3-tier spatial KPI/ledger UI, CSV export & batch analytics

EasyCopier.Tests/                       # Headless .NET 11 xUnit test suite mirroring Shared/ and Features/
```

### Slice Decoupling Rules
- Feature slices under `Features/<Slice>/` never depend directly on ViewModels or internal services of other slices.
- Cross-slice communication uses immutable `sealed record` messages over `IMessenger` (`EnqueueTransferRequestMessage`, `TransferBatchCompletedMessage`, `SettingsChangedMessage`).
- Cross-tab library contracts (`GameEntry`, `LibraryCategory`, `ILibrarySelectionCoordinator`, `LibraryCardControl`, `IFlyoutContentProvider`) live under `Features/Shell/Contracts/`.
- ViewModels and business/data services keep out of `Microsoft.UI.*` XAML types so `EasyCopier.Tests` runs headlessly on `net11.0`.

## Prerequisites

- **Windows 10 (build 22621+) or Windows 11** (`x64`)
- **.NET 11 SDK** with Windows App SDK workload/packages

## Building & Running Tests

```powershell
# Build solution (Release x64)
dotnet build Easy-Copier-v2.slnx --configuration Release

# Run headless unit and slice integration tests
dotnet test EasyCopier.Tests/EasyCopier.Tests.csproj --configuration Release --no-build
```

## Contributing & Verification

After making code changes, ensure zero warnings across both projects and verify headless tests:

```powershell
dotnet clean Easy-Copier-v2.slnx
dotnet build Easy-Copier-v2.slnx --configuration Release -p:Platform=x64
dotnet test EasyCopier.Tests/EasyCopier.Tests.csproj --configuration Release --no-build
```