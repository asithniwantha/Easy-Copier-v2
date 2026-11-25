using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Shared.Configuration;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.UI.Helpers;

namespace EasyCopier.Features.Shell.Contracts;

/// <summary>
/// Implements <see cref="ILibrarySelectionCoordinator"/> managing category selection dictionaries and pricing calculations.
/// </summary>
public sealed class LibrarySelectionCoordinator : ILibrarySelectionCoordinator, IRecipient<SettingsChangedMessage>
{
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<LibraryCategory, List<GameEntry>> _selections = new()
    {
        [LibraryCategory.Game] = [],
        [LibraryCategory.App] = [],
        [LibraryCategory.TvAndFilm] = [],
        [LibraryCategory.OsImage] = []
    };

    private bool _isOsImagesTabActive;
    private AppSettings? _cachedSettings;

    /// <inheritdoc />
    public event EventHandler? SelectionChanged;

    /// <inheritdoc />
    public event EventHandler? ClearRequested;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibrarySelectionCoordinator"/> class.
    /// </summary>
    public LibrarySelectionCoordinator(ISettingsService settingsService, IMessenger? messenger = null)
    {
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        messenger?.Register(this);
    }

    /// <inheritdoc />
    public IReadOnlyList<GameEntry> SelectedEntries => GetEffectiveSelectedEntries();

    /// <inheritdoc />
    public int SelectedCount => SelectedEntries.Count;

    /// <inheritdoc />
    public long SelectedTotalBytes => SelectedEntries.Sum(e => e.TotalBytes);

    /// <inheritdoc />
    public int SelectedTotalPrice
    {
        get
        {
            _cachedSettings ??= _settingsService.LoadSettingsSync();
            return SelectedEntries.Sum(e => FormattingHelpers.CalculatePrice(e.TotalBytes, _cachedSettings));
        }
    }

    /// <inheritdoc />
    public bool HasSelection => SelectedCount > 0;

    /// <inheritdoc />
    public void SetCategorySelection(LibraryCategory category, IEnumerable<GameEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _selections[category] = [.. entries];
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void SetActiveTab(bool isOsImagesTabActive)
    {
        if (_isOsImagesTabActive != isOsImagesTabActive)
        {
            _isOsImagesTabActive = isOsImagesTabActive;
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <inheritdoc />
    public void ClearAll()
    {
        foreach (LibraryCategory key in _selections.Keys.ToList())
        {
            _selections[key].Clear();
        }

        ClearRequested?.Invoke(this, EventArgs.Empty);
        SelectionChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <inheritdoc />
    public void Receive(SettingsChangedMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        _cachedSettings = null;
        if (message.PriceTiersUpdated && HasSelection)
        {
            SelectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private List<GameEntry> GetEffectiveSelectedEntries()
    {
        return _isOsImagesTabActive
            ? _selections[LibraryCategory.OsImage]
            : [
                .. _selections[LibraryCategory.Game],
                .. _selections[LibraryCategory.App],
                .. _selections[LibraryCategory.TvAndFilm]
            ];
    }
}

