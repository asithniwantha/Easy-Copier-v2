# GitHub Copilot Instructions — Easy Copier (v2 Architecture)

## 1. Repository & Technology Stack

- **Solution & Projects:** `Easy Copier.slnx` containing `Easy Copier/` (main WinUI 3 app) and `Easy Copier.Tests/` (headless unit/slice tests).
- **Runtime & Language:** .NET 10, C# 14, WinUI 3 (Windows App SDK), target platform `x64`, Windows 11 24H2+.
- **MVVM Toolkit:** `CommunityToolkit.Mvvm` (`ObservableObject`, `ObservableRecipient`, `[ObservableProperty]`, `[RelayCommand]`, `WeakReferenceMessenger`).
- **Architecture:** Single-Project Vertical Slice Architecture (`Features/` + `Shared/`).
- **Persistence & Packaging:** Local SQLite (`%AppData%\EasyCopier\history.db`), JSON settings (`appsettings.json`), JSON library cache (`library_cache.json`), and Velopack updates.

---

## 2. Exact Namespace Rules

### Root Namespace Rule
- **Current Root Namespace:** `Easy_Copier` (matches `Easy Copier.csproj` default namespace).
- **Never mix `Easy_Copier` and `EasyCopier`** in C# `namespace` declarations, `using` directives, or XAML `x:Class` / `xmlns` attributes.
  - Only switch the prefix from `Easy_Copier` to `EasyCopier` if `<RootNamespace>EasyCopier</RootNamespace>` has been explicitly set in `Easy Copier.csproj`.
- Always use **file-scoped namespaces** (`namespace Easy_Copier.Features.Library.Scanning;`) and mirror the folder hierarchy 1:1.

### Canonical Folder-to-Namespace Mapping

| Folder Path (under `Easy Copier/`) | Exact C# Namespace | Purpose |
| :--- | :--- | :--- |
| `/` (project root) | `Easy_Copier` | `App.xaml`, `CompositionRoot.cs` |
| `Features/Shell/` | `Easy_Copier.Features.Shell` | `MainWindow`, `MainPage`, `ShellViewModel`, `ShellSliceExtensions` |
| `Features/Shell/Contracts/` | `Easy_Copier.Features.Shell.Contracts` | `ILibraryTabView`, `ISearchableTabViewModel`, `LibraryTabViewModelBase`, `FileSystemItem` |
| `Features/Library/` | `Easy_Copier.Features.Library` | `LibrarySliceExtensions` |
| `Features/Library/Tabs/` | `Easy_Copier.Features.Library.Tabs` | `GamesTabView`, `AppsTabView`, `TvAndFilmsTabView` + ViewModels |
| `Features/Library/Details/` | `Easy_Copier.Features.Library.Details` | `GameDetailsFlyout`, `GameDetailsViewModel` |
| `Features/Library/Scanning/` | `Easy_Copier.Features.Library.Scanning` | `LibraryScannerService`, `GameScannerService`, `SourceLibraryService`, `LibraryCacheService` |
| `Features/Library/Metadata/` | `Easy_Copier.Features.Library.Metadata` | `GameInfoDownloadService`, `SysReqFormatter` |
| `Features/Library/Models/` | `Easy_Copier.Features.Library.Models` | Library-only models/enums (`GameEntry`, `LibraryCategory`, `GameCategory`) |
| `Features/Library/Converters/` | `Easy_Copier.Features.Library.Converters` | Library-only converters (`CategoryFormatConverter`, `GameCategoryToVisibilityConverter`) |
| `Features/OsImageLauncher/` | `Easy_Copier.Features.OsImageLauncher` | `OsImagesTabView`, `OsImagesTabViewModel`, `OsImageDetailsFlyout`, `RufusResolutionHelper`, `IsoBootstrapperService`, `OsImageLauncherSliceExtensions` |
| `Features/TransferQueue/` | `Easy_Copier.Features.TransferQueue` | `TransferQueueService`, `TransferQueueViewModel`, `DriveDiscoveryService`, `DriveValidationService`, `WindowsShellTransferService`, `ConflictDialogContent`, `TransferQueueSliceExtensions` |
| `Features/TransferQueue/Models/` | `Easy_Copier.Features.TransferQueue.Models` | `TransferQueueItem`, `RemovableDrive` |
| `Features/History/` | `Easy_Copier.Features.History` | `HistoryWindow`, `HistoryPage`, `HistoryViewModel`, `CopyHistoryService`, `ReportService`, `HistorySliceExtensions` |
| `Features/SmartAdder/` | `Easy_Copier.Features.SmartAdder` | `SmartAdderControl`, `SmartAdderViewModel`, `SmartAdderHistoryWindow`, `SmartAdderHistoryViewModel`, `SmartAdderHistoryService`, `SmartAdderSliceExtensions` |
| `Features/SmartAdder/Behaviors/` | `Easy_Copier.Features.SmartAdder.Behaviors` | `NumericTextBoxBehavior`, `HoverBehavior`, `FocusWithinBehavior` |
| `Features/SmartAdder/Models/` | `Easy_Copier.Features.SmartAdder.Models` | `SmartAdderEntry`, `NumberCell`, `SmartAdderHistoryRecord` |
| `Features/SmartAdder/Converters/` | `Easy_Copier.Features.SmartAdder.Converters` | `NegativeToBackgroundConverter` |
| `Features/Settings/` | `Easy_Copier.Features.Settings` | `SettingsWindow`, `SettingsViewModel`, `StartupService`, `UpdateService`, `SettingsSliceExtensions` |
| `Features/Settings/About/` | `Easy_Copier.Features.Settings.About` | `AboutWindow`, `AboutViewModel` |
| `Shared/` | `Easy_Copier.Shared` | `SharedServiceExtensions` |
| `Shared/Configuration/` | `Easy_Copier.Shared.Configuration` | `AppSettings`, `ISettingsService`, `SettingsService` |
| `Shared/Messages/` | `Easy_Copier.Shared.Messages` | Immutable cross-slice `WeakReferenceMessenger` records |
| `Shared/Persistence/` | `Easy_Copier.Shared.Persistence` | `ISqliteConnectionFactory`, `SqliteConnectionFactory` |
| `Shared/Platform/` | `Easy_Copier.Shared.Platform` | `AppWindowContext`, `FolderPickerService`, `FilePickerService`, `WindowService`, `DialogService`, `DispatcherService`, `AudioPlaybackService`, `ProcessService` |
| `Shared/Interop/` | `Easy_Copier.Shared.Interop` | `FileOperationInterop`, `IFileOperationProgressSink`, `IShellItem`, `NativeWindowHelper` |
| `Shared/UI/Converters/` | `Easy_Copier.Shared.UI.Converters` | `BoolToVisibilityConverter`, `BytesToSizeConverter`, `GameSizeToPriceConverter` |
| `Shared/UI/Helpers/` | `Easy_Copier.Shared.UI.Helpers` | `FlyoutHelper`, `FormattingHelpers`, `FileSystemHelpers`, `ObservableCollectionExtensions` |

### Test Project Namespace Mapping
- Mirror the production directory tree inside `Easy Copier.Tests/`:
  - `Easy Copier.Tests/Features/<Slice>/` -> `namespace Easy_Copier.Tests.Features.<Slice>;`
  - `Easy Copier.Tests/Shared/<Area>/` -> `namespace Easy_Copier.Tests.Shared.<Area>;`

### XAML Namespace & `x:Class` Rules
- Every `.xaml` file's `x:Class` MUST match its code-behind namespace and class name exactly:
  ```xml
  x:Class="Easy_Copier.Features.TransferQueue.ConflictDialogContent"
  ```
- Use `using:` syntax for local WinUI 3 XML namespaces (never `clr-namespace:`):
  ```xml
  xmlns:converters="using:Easy_Copier.Shared.UI.Converters"
  xmlns:behaviors="using:Easy_Copier.Features.SmartAdder.Behaviors"
  ```
- When moving any `.xaml` file or converter, immediately update `App.xaml` ResourceDictionary registrations and all consuming XAML `xmlns:` prefixes to prevent `WMC1506` and XAML compiler errors.

---

## 3. Architectural Guardrails

### 1. Strict Slice Independence (Allowed Import Matrix)
- `Features/<SliceA>/` **MAY** reference:
  - `Easy_Copier.Shared.*`
  - `Easy_Copier.Features.Shell.Contracts` (for tab views/ViewModels)
  - Types inside its own `Features/<SliceA>.*` subtree
- `Features/<SliceA>/` **MUST NEVER** reference ViewModels, Views, or private services from `Features/<SliceB>/`.
  - Exception: `Features/Shell/` hosts top-level tabs and binds `TransferQueueViewModel` / feature views as the application shell, and `CompositionRoot.cs` wires all `Add<Slice>Slice()` DI extensions.

### 2. Rule of Three for `Shared/`
- Place new models, helpers, behaviors, and converters inside the owning `Features/<Slice>/` folder by default.
- Only promote a type to `Shared/` when **3 or more distinct slices** depend on it (e.g., `SettingsService`, `BytesToSizeConverter`, `SqliteConnectionFactory`).

### 3. Plain Dependency Injection (No MediatR)
- Do **not** install or introduce MediatR or command-bus indirection.
- Inject interfaces and handlers directly via primary or explicit constructors.
- Each slice exposes one static registration extension method in its root folder:
  - `AddSharedFoundation(this IServiceCollection services)`
  - `AddShellSlice(this IServiceCollection services)`
  - `AddLibrarySlice(this IServiceCollection services)`
  - `AddOsImageLauncherSlice(this IServiceCollection services)`
  - `AddTransferQueueSlice(this IServiceCollection services)`
  - `AddHistorySlice(this IServiceCollection services)`
  - `AddSmartAdderSlice(this IServiceCollection services)`
  - `AddSettingsSlice(this IServiceCollection services)`
- Eagerly resolve background message listeners (such as `ICopyHistoryService`) at startup in `CompositionRoot.cs` so they receive messages even when their UI windows are closed.

### 4. Cross-Slice Messaging (`WeakReferenceMessenger`)
- All cross-slice notifications must use immutable `readonly record struct` payloads defined in `Easy_Copier.Shared.Messages`:
  - `EnqueueTransferRequestMessage`
  - `TransferBatchCompletedMessage`
  - `DriveConnectionChangedMessage`
  - `SettingsChangedMessage`
- ViewModels subscribing to messages must inherit `ObservableRecipient` and set `IsActive = false` on `Window.Closed` or `FrameworkElement.Unloaded` to prevent memory leaks.

---

## 4. WinUI 3 & C# 14 Coding Standards

- **Avoid `MVVMTK0045`:** Use C# 14 `partial` properties with `[ObservableProperty]` (e.g., `[ObservableProperty] public partial bool IsBusy { get; set; }`) instead of annotated backing fields.
- **Elevated-Safe Storage Pickers:** Always use `Microsoft.Windows.Storage.Pickers` (`FolderPicker`, `FileOpenPicker`, `FileSavePicker`) initialized with `WindowId` from `IAppWindowContext`. Never introduce legacy `Windows.Storage.Pickers` + `InitializeWithWindow`, which fail when running as Administrator.
- **Windowing & Thread Marshalling:**
  - Open secondary windows (`SettingsWindow`, `HistoryWindow`, `AboutWindow`, `SmartAdderHistoryWindow`) exclusively via `IWindowService` with parent HWND ownership anchored to `MainWindow`.
  - Recreate closed secondary windows via DI rather than hiding/reusing closed WinUI `Window` instances.
  - Marshal all background transfer, file I/O, and WMI hardware event callbacks onto the UI thread through `IDispatcherService`.
- **3-Tier Reporting UI (`Features/History/HistoryPage.xaml`):**
  - Maintain the 3-tier layout: top KPI summary cards, central tabular workspace grid, and right-sliding `SplitView` (`PanePlacement="Right"`) contextual details drawer.
  - Right-align numeric columns (Size, Price, Duration) with `Typography.NumeralAlignment="Tabular"` and enforce `TextTrimming="CharacterEllipsis"` on long path/name cells.
- **Minimal Code-Behind:** Prefer `{x:Bind}` and `[RelayCommand]` over code-behind event handlers, limiting code-behind strictly to WinUI lifecycle hooks (`Loaded`/`Unloaded`/`Closed`) and focus/windowing glue.

---

## 5. Testing, Git & Verification Workflow

- **Headless CI Testability:** Keep calculations, FAT32/capacity validation, filtering, and SQLite repository logic free of WinUI/XAML types so `Easy Copier.Tests` runs headlessly via `dotnet test` in GitHub Actions.
- **SQLite Testing:** Inject `ISqliteConnectionFactory` so tests run against in-memory SQLite databases rather than `%AppData%\EasyCopier\history.db`.
- **Preserve Git History:** Always use `git mv` when relocating files during refactoring.
- **Mandatory Verification Gate:** After any structural move or code change, run:
  ```powershell
  dotnet clean "Easy Copier.slnx"   # Required after moving .xaml files to clear stale .g.i.cs caches
  dotnet build "Easy Copier.slnx"
  dotnet test "Easy Copier.slnx"
  ```
  Resolve all build errors and warnings (including `WMC1506` and `MVVMTK0045`) before completing a task.