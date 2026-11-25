# GitHub Copilot Instructions — Easy-Copier v2 Architecture

## 1. Repository & Technology Stack

- **Solution & Projects:** `Easy-Copier-v2.slnx` containing `Easy-Copier-v2/` (main WinUI 3 app) and `EasyCopier.Tests/` (headless unit/slice tests).
- **Runtime & Language:** .NET 11, C# 15, WinUI 3 (Windows App SDK), target platform `x64`, Windows 11 24H2+ (`TargetPlatformMinVersion` 10.0.22621.0).
- **Deployment Mode:** Unpackaged (`<WindowsPackageType>None</WindowsPackageType>`, `<WindowsAppSDKSelfContained>true</WindowsAppSDKSelfContained>`) with custom `Program.cs` (`DISABLE_XAML_GENERATED_MAIN`) for Velopack and Administrator elevation.
- **MVVM Toolkit:** `CommunityToolkit.Mvvm` (`ObservableObject`, `ObservableRecipient`, `[ObservableProperty]`, `[RelayCommand]`, `WeakReferenceMessenger`).
- **Architecture:** Single-Project Vertical Slice Architecture (`Features/` + `Shared/`).
- **Persistence & Packaging:** Local SQLite (`%AppData%\EasyCopier\history.db`), JSON settings (`appsettings.json`), JSON library cache (`library_cache.json`), and Velopack updates.

---

## 2. Exact Namespace Rules

### Root Namespace Rule
- **Current Root Namespace:** `EasyCopier` (explicitly set via `<RootNamespace>EasyCopier</RootNamespace>` in `Easy-Copier-v2.csproj`).
- **Never mix `Easy_Copier`, `Easy_Copier_v2`, and `EasyCopier`** in C# `namespace` declarations, `using` directives, or XAML `x:Class` / `xmlns` attributes.
- Always use **file-scoped namespaces** (`namespace EasyCopier.Features.Library.Scanning;`) and mirror the folder hierarchy 1:1.

### Canonical Folder-to-Namespace Mapping

| Folder Path (under `Easy-Copier-v2/`) | Exact C# Namespace | Purpose |
| :--- | :--- | :--- |
| `/` (project root) | `EasyCopier` | `Program.cs`, `App.xaml`, `CompositionRoot.cs` |
| `Features/Shell/` | `EasyCopier.Features.Shell` | `MainWindow`, `MainPage`, `ShellViewModel`, `ShellSliceExtensions` |
| `Features/Shell/Contracts/` | `EasyCopier.Features.Shell.Contracts` | `ILibraryTabView`, `ISearchableTabViewModel`, `LibraryTabViewModelBase`, `FileSystemItem`, `GameEntry`, `LibraryCategory`, `GameCategory`, `LibraryTabIndex`, `LibraryCardControl`, `AutoSuggestBoxSearchBehavior`, `LibraryViewExtensions`, `IFlyoutService`, `IFlyoutContentProvider`, `FlyoutHelper`, `CategoryFormatConverter`, `GameCategoryToVisibilityConverter` |
| `Features/Library/` | `EasyCopier.Features.Library` | `LibrarySliceExtensions`, `LibraryCatalogService` |
| `Features/Library/Tabs/` | `EasyCopier.Features.Library.Tabs` | `GamesTabView`, `AppsTabView`, `TvAndFilmsTabView` + ViewModels |
| `Features/Library/Details/` | `EasyCopier.Features.Library.Details` | `GameDetailsFlyout`, `GameDetailsViewModel`, `VideoDetailsFlyout`, `VideoDetailsViewModel`, `LibraryFlyoutContentProvider` |
| `Features/Library/Duplicates/` | `EasyCopier.Features.Library.Duplicates` | `DuplicateManagerWindow`, `DuplicateManagerViewModel`, `SafeDeletionService` |
| `Features/Library/Scanning/` | `EasyCopier.Features.Library.Scanning` | `LibraryScannerService`, `GameScannerService`, `SourceLibraryService`, `LibraryCacheService`, `LibraryFilterService`, `LibrarySelectionCoordinator`, `ThumbnailService`, `GameTitleSanitizer` |
| `Features/Library/Metadata/` | `EasyCopier.Features.Library.Metadata` | `GameInfoDownloadService`, `GameRequirementsService`, `SysReqFormatter` |
| `Features/Library/Models/` | `EasyCopier.Features.Library.Models` | Library-only models (`LibraryCacheSnapshot`, `ItemFingerprint`, `CacheValidationOutcome`, `TvAndFilmsSortOption`, `DuplicateGroup`) |
| `Features/OsImageLauncher/` | `EasyCopier.Features.OsImageLauncher` | `OsImagesTabView`, `OsImagesTabViewModel`, `OsImageDetailsFlyout`, `RufusResolutionHelper`, `IsoBootstrapperService`, `OsImageLauncherSliceExtensions` |
| `Features/TransferQueue/` | `EasyCopier.Features.TransferQueue` | `TransferQueueService`, `TransferQueueViewModel`, `DriveDiscoveryService`, `DriveValidationService`, `WindowsShellTransferService`, `ConflictDialogContent`, `TransferQueueSliceExtensions` |
| `Features/TransferQueue/Models/` | `EasyCopier.Features.TransferQueue.Models` | `TransferQueueItem`, `RemovableDrive` |
| `Features/History/` | `EasyCopier.Features.History` | `HistoryWindow`, `HistoryPage`, `HistoryViewModel`, `CopyHistoryService`, `ReportService`, `HistorySliceExtensions` |
| `Features/SmartAdder/` | `EasyCopier.Features.SmartAdder` | `SmartAdderControl`, `SmartAdderViewModel`, `SmartAdderHistoryWindow`, `SmartAdderHistoryViewModel`, `SmartAdderHistoryService`, `SmartAdderSliceExtensions` |
| `Features/SmartAdder/Behaviors/` | `EasyCopier.Features.SmartAdder.Behaviors` | `NumericTextBoxBehavior`, `HoverBehavior`, `FocusWithinBehavior` |
| `Features/SmartAdder/Models/` | `EasyCopier.Features.SmartAdder.Models` | `SmartAdderEntry`, `NumberCell`, `SmartAdderHistoryRecord` |
| `Features/SmartAdder/Converters/` | `EasyCopier.Features.SmartAdder.Converters` | `NegativeToBackgroundConverter` |
| `Features/Settings/` | `EasyCopier.Features.Settings` | `SettingsWindow`, `SettingsViewModel`, `StartupService`, `UpdateService`, `SettingsSliceExtensions` |
| `Features/Settings/About/` | `EasyCopier.Features.Settings.About` | `AboutWindow`, `AboutViewModel` |
| `Shared/` | `EasyCopier.Shared` | `SharedServiceExtensions` |
| `Shared/Configuration/` | `EasyCopier.Shared.Configuration` | `AppSettings`, `CopyVerificationMode`, `ISettingsService`, `SettingsService` |
| `Shared/Messages/` | `EasyCopier.Shared.Messages` | Immutable cross-slice `WeakReferenceMessenger` `sealed record` messages |
| `Shared/Models/` | `EasyCopier.Shared.Models` | Shared domain contracts (`ValidationResult`, `ValidationSeverity`, `PagedResult<T>`, `SourceFolder`, `SourceFolderCategory`) |
| `Shared/Persistence/` | `EasyCopier.Shared.Persistence` | `ISqliteConnectionFactory`, `SqliteConnectionFactory` |
| `Shared/Platform/` | `EasyCopier.Shared.Platform` | `AppWindowContext`, `FolderPickerService`, `FilePickerService`, `WindowService` (`WindowRegistration`), `DialogService`, `DispatcherService`, `AudioPlaybackService`, `ProcessService`, `FileSystemService`, `ClipboardService`, `SystemTrayService` (`ITransferActivityMonitor`) |
| `Shared/Interop/` | `EasyCopier.Shared.Interop` | `FileOperationInterop`, `IFileOperationProgressSink`, `IShellItem`, `NativeWindowHelper` |
| `Shared/UI/Converters/` | `EasyCopier.Shared.UI.Converters` | `BoolToVisibilityConverter`, `BytesToSizeConverter`, `GameSizeToPriceConverter` |
| `Shared/UI/Helpers/` | `EasyCopier.Shared.UI.Helpers` | `FlyoutHelper`, `FormattingHelpers`, `FileSystemHelpers`, `ObservableCollectionExtensions` |

### Test Project Namespace Mapping
- Mirror the production directory tree inside `EasyCopier.Tests/`:
  - `EasyCopier.Tests/Features/<Slice>/` -> `namespace EasyCopier.Tests.Features.<Slice>;`
  - `EasyCopier.Tests/Shared/<Area>/` -> `namespace EasyCopier.Tests.Shared.<Area>;`

### XAML Namespace & `x:Class` Rules
- Every `.xaml` file's `x:Class` MUST match its code-behind namespace and class name exactly:
  ```xml
  x:Class="EasyCopier.Features.TransferQueue.ConflictDialogContent"
  ```
- Use `using:` syntax for local WinUI 3 XML namespaces (never `clr-namespace:`):
  ```xml
  xmlns:converters="using:EasyCopier.Shared.UI.Converters"
  xmlns:behaviors="using:EasyCopier.Features.SmartAdder.Behaviors"
  ```
- When moving any `.xaml` file or converter, immediately update `App.xaml` ResourceDictionary registrations and all consuming XAML `xmlns:` prefixes to prevent `WMC1506` and XAML compiler errors.

---

## 3. Architectural Guardrails

### 1. Strict Slice Independence (Allowed Import Matrix)
- `Features/<SliceA>/` **MAY** reference:
  - `EasyCopier.Shared.*`
  - `EasyCopier.Features.Shell.Contracts` (for tab views/ViewModels)
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
- All cross-slice notifications must use immutable `sealed record` payloads (required by `CommunityToolkit.Mvvm`'s `IRecipient<TMessage>` `where TMessage : class` constraint; nested item DTOs such as `TransferSourceItem` use `readonly record struct`) defined in `EasyCopier.Shared.Messages`:
  - `EnqueueTransferRequestMessage`
  - `TransferBatchCompletedMessage`
  - `DriveConnectionChangedMessage`
  - `SettingsChangedMessage`
- ViewModels subscribing to messages must inherit `ObservableRecipient` and set `IsActive = false` on `Window.Closed` or `FrameworkElement.Unloaded` to prevent memory leaks.

---

## 4. WinUI 3 & C# 15 Coding Standards

- **Avoid `MVVMTK0045`:** Use C# 15 `partial` properties with `[ObservableProperty]` (e.g., `[ObservableProperty] public partial bool IsBusy { get; set; }`) instead of annotated backing fields.
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

- **Headless CI Testability:** Keep calculations, FAT32/capacity validation, filtering, and SQLite repository logic free of WinUI/XAML types so `EasyCopier.Tests` runs headlessly via `dotnet test` in GitHub Actions.
- **SQLite Testing:** Inject `ISqliteConnectionFactory` so tests run against in-memory SQLite databases rather than `%AppData%\EasyCopier\history.db`.
- **Branch Per Task (Ask First):** Before making any code-changing task's first edit, ask the user whether to create a new GitHub branch for it. Do not edit files until the user answers.
  - If the user agrees, propose a short descriptive name (`feature/<topic>`, `fix/<topic>`, `refactor/<topic>`, `chore/<topic>`), run `git switch -c <branch-name>` from an up-to-date `main`, and make all changes on that branch.
  - If uncommitted changes exist on the current branch, tell the user before switching and ask whether to carry them over, stash them, or commit them first.
  - Never commit or push directly to `main`. Push the branch (`git push -u origin <branch-name>`) only when the user asks.
  - If the user declines, continue on the current branch (unless it is `main`, in which case confirm again).
  - Read-only questions and explanations do not require a branch.
- **Preserve Git History:** Always use `git mv` when relocating files during refactoring.
- **Mandatory Verification Gate:** After any structural move or code change, run:
  ```powershell
  dotnet clean Easy-Copier-v2.slnx   # Required after moving .xaml files to clear stale .g.i.cs caches
  dotnet build Easy-Copier-v2.slnx
  dotnet test Easy-Copier-v2.slnx
  ```
  Resolve all build errors and warnings (including `WMC1506` and `MVVMTK0045`) before completing a task.