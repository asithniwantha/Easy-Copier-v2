# Easy-Copier v2 Architecture Rules

Follow all architecture, namespace, and verification rules defined in `.github/copilot-instructions.md`.

## Stack & Constraints
- Framework: WinUI 3 / Windows App SDK, .NET 11, C# 15, x64 (unpackaged via `WindowsPackageType=None` and `Program.cs` Velopack entry point).
- Root Namespace: `EasyCopier` (matches `<RootNamespace>EasyCopier</RootNamespace>` in `Easy-Copier-v2.csproj`).
- MVVM: CommunityToolkit.Mvvm (use partial properties for `[ObservableProperty]` to avoid `MVVMTK0045`; prefer `ICommand` bindings over code-behind event handlers).
- Architecture: Single-Project Vertical Slice Architecture (`Features/` + `Shared/`).

## Strict Guardrails
1. Rule of Three for `Shared/`: Only place code in `Shared/` if 3+ slices use it. Keep feature-specific models, converters, and behaviors inside `Features/<Slice>/`.
2. No Cross-Slice Coupling: A slice under `Features/<A>/` must NEVER reference ViewModels or internal services in `Features/<B>/`. Use immutable `sealed record` messages in `Shared/Messages/` via `WeakReferenceMessenger` (required by `CommunityToolkit.Mvvm`'s `where TMessage : class` constraint; nested item DTOs like `TransferSourceItem` use `readonly record struct`). Place cross-tab library contracts (`GameEntry`, `LibraryCategory`, `GameCategory`, `LibraryCardControl`, `AutoSuggestBoxSearchBehavior`, `IFlyoutContentProvider`) in `Features/Shell/Contracts/` so `OsImageLauncher` and `Library` stay decoupled.
3. Plain DI Only: Do NOT install or use MediatR. Inject services/handlers directly via constructors. Every slice exports one DI extension method: `Add<SliceName>Slice(this IServiceCollection services)`.
4. Namespace & XAML Sync: Whenever moving a `.cs` or `.xaml` file, update its C# namespace, `x:Class`, `xmlns:` prefixes, and `App.xaml` ResourceDictionary references immediately.
5. Headless Testability: Keep calculations, validation, and SQLite logic free of WinUI/XAML types so `EasyCopier.Tests` runs headlessly via `dotnet test`.
6. Verification Gate: After every task, run `dotnet build Easy-Copier-v2.slnx` and `dotnet test Easy-Copier-v2.slnx` and fix all warnings/errors before finishing.