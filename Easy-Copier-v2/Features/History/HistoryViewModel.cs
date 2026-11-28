using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Features.History.Models;
using EasyCopier.Shared.Messages;
using EasyCopier.Shared.Models;
using EasyCopier.Shared.Platform;
using EasyCopier.Shared.UI.Helpers;
using Microsoft.Extensions.Logging;

namespace EasyCopier.Features.History;

/// <summary>
/// Specifies predefined date filtering options for file copy history records.
/// </summary>
public enum DateFilter
{
    Today,
    Last7Days,
    ThisMonth,
    Custom
}

/// <summary>
/// Specifies tri-fold outcome status filtering options for file copy history records.
/// </summary>
public enum StatusFilter
{
    All,
    Completed,
    Failed,
    Skipped
}

/// <summary>
/// ViewModel for managing file transfer history records, filtering options, aggregated statistics,
/// spatial reporting density, and CSV exports.
/// </summary>
public partial class HistoryViewModel
    : ObservableRecipient, IRecipient<TransferBatchCompletedMessage>
{
    private readonly ICopyHistoryService _copyHistoryService;
    private readonly IReportService _reportService;
    private readonly IFilePickerService _filePickerService;
    private readonly IHistoryAnalysisService _historyAnalysisService;
    private readonly IDispatcherService _dispatcherService;
    private readonly ILogger<HistoryViewModel>? _logger;

    private bool _isInitialized;

    public const string FilterToday = "Today";
    public const string FilterLast7Days = "Last 7 Days";
    public const string FilterThisMonth = "This Month";
    public const string FilterAllTime = "All Time";

    public const string StatusAll = "All";
    public const string StatusCompleted = "Completed";
    public const string StatusFailed = "Failed";
    public const string StatusSkipped = "Skipped";

    private static readonly string[] DefaultDateFilterStrings = [FilterToday, FilterLast7Days, FilterThisMonth, FilterAllTime];
    private static readonly string[] DefaultStatusFilterStrings = [StatusAll, StatusCompleted, StatusFailed, StatusSkipped];

    [ObservableProperty]
    public partial HistoryStats SelectedFilterStats { get; set; } = new(0, 0, 0, 0, "0%");

    [ObservableProperty]
    public partial ObservableCollection<CopyHistoryRecord> Records { get; set; } = new BulkObservableCollection<CopyHistoryRecord>();

    [ObservableProperty]
    public partial CopyHistoryRecord? SelectedRecord { get; set; }

    [ObservableProperty]
    public partial string StatusMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string HeaderSubtitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasNoRecords { get; set; }

    [ObservableProperty]
    public partial string LastUpdatedText { get; set; } = "\u2014";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DensityLabel))]
    public partial bool IsCompactDensity { get; set; }

    /// <summary>Gets the active row density label for the UI toggle button.</summary>
    public string DensityLabel => IsCompactDensity ? "Compact" : "Comfortable";

    public IReadOnlyList<DateFilter> AvailableDateFilters { get; } = Enum.GetValues<DateFilter>();
    public IReadOnlyList<StatusFilter> AvailableStatusFilters { get; } = Enum.GetValues<StatusFilter>();

    [ObservableProperty]
    public partial DateFilter SelectedDateFilter { get; set; } = DateFilter.Today;

    [ObservableProperty]
    public partial StatusFilter SelectedStatusFilter { get; set; } = StatusFilter.All;

    [ObservableProperty]
    public partial ObservableCollection<string> AvailableDateFilterStrings { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> AvailableStatusFilterStrings { get; set; }

    [ObservableProperty]
    public partial string SelectedDateFilterString { get; set; }

    [ObservableProperty]
    public partial string SelectedStatusFilterString { get; set; }

    [ObservableProperty]
    public partial int CurrentPage { get; set; } = 1;

    [ObservableProperty]
    public partial int TotalPages { get; set; } = 1;

    [ObservableProperty]
    public partial int TotalRecordCount { get; set; }

    [ObservableProperty]
    public partial int PageSize { get; set; } = 50;

    [ObservableProperty]
    public partial bool HasNextPage { get; set; }

    [ObservableProperty]
    public partial bool HasPreviousPage { get; set; }

    [ObservableProperty]
    public partial string PaginationSummaryText { get; set; } = "Page 1 of 1";

    [ObservableProperty]
    public partial string TotalRecordSummaryText { get; set; } = "0 transfers found";

    public IReadOnlyList<int> AvailablePageSizes { get; } = (int[])[25, 50, 100, 200];

    [ObservableProperty]
    public partial int SelectedPageSize { get; set; } = 50;

    [ObservableProperty]
    public partial bool IsDrawerOpen { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<string> SelectedRecordSubFiles { get; set; } = [];

    public IReadOnlyList<string> DashboardRangeOptions { get; } = (string[])["Last 7 Days", "Last 30 Days", "Last 90 Days"];

    [ObservableProperty]
    public partial string SelectedDashboardRange { get; set; } = "Last 7 Days";

    [ObservableProperty]
    public partial DashboardStats? Dashboard { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<PeriodTotal> DailyTotals { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<PeriodTotal> WeeklyTotals { get; set; } = [];

    [ObservableProperty]
    public partial ObservableCollection<TopCopiedItem> TopItems { get; set; } = [];

    [ObservableProperty]
    public partial string FailureRateText { get; set; } = "0%";

    [ObservableProperty]
    public partial bool IsDashboardVisible { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HistoryViewModel"/> class.
    /// </summary>
    public HistoryViewModel(
        ICopyHistoryService copyHistoryService,
        IReportService reportService,
        IFilePickerService filePickerService,
        IHistoryAnalysisService historyAnalysisService,
        IDispatcherService dispatcherService,
        IMessenger messenger,
        ILogger<HistoryViewModel>? logger = null)
        : base(messenger)
    {
        _copyHistoryService = copyHistoryService ?? throw new ArgumentNullException(nameof(copyHistoryService));
        _reportService = reportService ?? throw new ArgumentNullException(nameof(reportService));
        _filePickerService = filePickerService ?? throw new ArgumentNullException(nameof(filePickerService));
        _historyAnalysisService = historyAnalysisService ?? throw new ArgumentNullException(nameof(historyAnalysisService));
        _dispatcherService = dispatcherService ?? throw new ArgumentNullException(nameof(dispatcherService));
        _logger = logger;

        AvailableDateFilterStrings = [.. DefaultDateFilterStrings];
        AvailableStatusFilterStrings = [.. DefaultStatusFilterStrings];
        SelectedDateFilterString = FilterToday;
        SelectedStatusFilterString = StatusAll;
    }

    protected override void OnActivated()
    {
        Messenger.Register<TransferBatchCompletedMessage>(this);
        _copyHistoryService.HistoryChanged += OnServiceHistoryChanged;
    }

    protected override void OnDeactivated()
    {
        _copyHistoryService.HistoryChanged -= OnServiceHistoryChanged;
        Messenger.UnregisterAll(this);
    }

    /// <summary>Asynchronously loads history records from storage and applies active filters.</summary>
    public async Task InitializeAsync()
    {
        IsActive = true;

        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        await LoadPageDataAsync();
        await RefreshDashboardAsync();
    }

    /// <inheritdoc />
    public void Receive(TransferBatchCompletedMessage message)
    {
        RefreshAfterChange();
    }

    private void OnServiceHistoryChanged(object? sender, EventArgs e)
    {
        RefreshAfterChange();
    }

    private void RefreshAfterChange()
    {
        if (!IsActive || !_isInitialized)
        {
            return;
        }

        _ = _dispatcherService.TryEnqueue(async () =>
        {
            await LoadPageDataAsync();
            await RefreshDashboardAsync();
        });
    }

    [RelayCommand]
    private void ToggleDensity() => IsCompactDensity = !IsCompactDensity;

    partial void OnSelectedPageSizeChanged(int value)
    {
        if (value > 0 && PageSize != value)
        {
            PageSize = value;
            CurrentPage = 1;
            _ = LoadPageDataAsync();
        }
    }

    partial void OnSelectedDashboardRangeChanged(string value) => _ = RefreshDashboardAsync();

    [RelayCommand]
    private void ToggleDashboard() => IsDashboardVisible = !IsDashboardVisible;

    private async Task RefreshDashboardAsync()
    {
        if (!_isInitialized)
        {
            return;
        }

        int days = SelectedDashboardRange switch
        {
            "Last 30 Days" => 30,
            "Last 90 Days" => 90,
            _ => 7
        };

        DateTime minDate = DateTime.Today.AddDays(-days);
        DateTime maxDate = DateTime.Today.AddDays(1);
        List<CopyHistoryRecord> records = await _copyHistoryService.GetRecordsByWeekAsync(minDate, maxDate);

        DashboardStats stats = _historyAnalysisService.BuildDashboard(records, days);
        Dashboard = stats;
        DailyTotals.UpdateFrom(stats.DailyTotals);
        WeeklyTotals.UpdateFrom(stats.WeeklyTotals);
        TopItems.UpdateFrom(stats.TopItems);
        FailureRateText = $"{Math.Round(stats.OverallFailureRate, 1)}%";
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilters();

    partial void OnSelectedDateFilterChanged(DateFilter value) => ApplyFilters();

    partial void OnSelectedStatusFilterChanged(StatusFilter value) => ApplyFilters();

    partial void OnSelectedDateFilterStringChanged(string value)
    {
        SelectedDateFilter = value switch
        {
            FilterToday => DateFilter.Today,
            FilterLast7Days => DateFilter.Last7Days,
            FilterThisMonth => DateFilter.ThisMonth,
            _ => DateFilter.Custom
        };
    }

    partial void OnSelectedStatusFilterStringChanged(string value)
    {
        SelectedStatusFilter = value switch
        {
            StatusCompleted => StatusFilter.Completed,
            StatusFailed => StatusFilter.Failed,
            StatusSkipped => StatusFilter.Skipped,
            _ => StatusFilter.All
        };
    }

    partial void OnSelectedRecordChanged(CopyHistoryRecord? value)
    {
        if (value != null)
        {
            IsDrawerOpen = true;
            SelectedRecordSubFiles.Clear();
            try
            {
                if (!string.IsNullOrWhiteSpace(value.SubFilesJson) && value.SubFilesJson != "[]")
                {
                    List<string>? files = JsonSerializer.Deserialize<List<string>>(value.SubFilesJson);
                    if (files != null)
                    {
                        SelectedRecordSubFiles.UpdateFrom(files);
                    }
                }
            }
            catch (Exception ex) when (ex is JsonException or NotSupportedException)
            {
                _logger?.LogWarning(ex, "Failed to deserialize sub-files JSON for history record ID {RecordId}", value.Id);
            }
        }
        else
        {
            IsDrawerOpen = false;
        }
    }

    [RelayCommand]
    private void CloseDrawer() => SelectedRecord = null;

    private void ApplyFilters()
    {
        if (!_isInitialized)
        {
            return;
        }

        CurrentPage = 1;
        _ = LoadPageDataAsync();
    }

    private (DateTime? StartDate, DateTime? EndDate, TransferStatus? Status, string? Search) BuildQueryFilters()
    {
        DateTime now = DateTime.Now;
        DateTime? startDate = SelectedDateFilter switch
        {
            DateFilter.Today => now.Date,
            DateFilter.Last7Days => now.AddDays(-7).Date,
            DateFilter.ThisMonth => new DateTime(now.Year, now.Month, 1),
            _ => null
        };

        DateTime? endDate = SelectedDateFilter switch
        {
            DateFilter.Today => now.Date.AddDays(1),
            DateFilter.Last7Days => now.Date.AddDays(1),
            DateFilter.ThisMonth => new DateTime(now.Year, now.Month, 1).AddMonths(1),
            _ => null
        };

        TransferStatus? statusFilter = SelectedStatusFilter switch
        {
            StatusFilter.Completed => TransferStatus.Completed,
            StatusFilter.Failed => TransferStatus.Failed,
            StatusFilter.Skipped => TransferStatus.Skipped,
            _ => null
        };

        string? search = string.IsNullOrWhiteSpace(SearchQuery) ? null : SearchQuery.Trim();
        return (startDate, endDate, statusFilter, search);
    }

    private async Task LoadPageDataAsync()
    {
        if (!_isInitialized)
        {
            return;
        }

        StatusMessage = "Loading page...";
        (DateTime? startDate, DateTime? endDate, TransferStatus? statusFilter, string? search) = BuildQueryFilters();

        Task<PagedResult<CopyHistoryRecord>> pageTask = _copyHistoryService.GetPagedRecordsAsync(
            startDate, endDate, statusFilter, search, CurrentPage, PageSize);
        Task<(int TotalItems, int SuccessfulItems, long TotalBytes, int TotalAmount)> statsTask = _copyHistoryService.GetFilteredStatsAsync(
            startDate, endDate, statusFilter, search);

        await Task.WhenAll(pageTask, statsTask);

        PagedResult<CopyHistoryRecord> pagedResult = await pageTask;
        (int totalItems, int successfulItems, long totalBytes, int totalAmount) = await statsTask;

        TotalRecordCount = pagedResult.TotalCount;
        TotalPages = Math.Max(1, pagedResult.TotalPages);
        HasNextPage = pagedResult.HasNextPage;
        HasPreviousPage = pagedResult.HasPreviousPage;

        PaginationSummaryText = $"Page {CurrentPage} of {TotalPages}";
        TotalRecordSummaryText = $"{TotalRecordCount:N0} transfer(s) found";

        List<CopyHistoryRecord> pageItems = [.. pagedResult.Items];
        _historyAnalysisService.ApplyBatchClustering(pageItems);
        Records.UpdateFrom(pageItems);

        string successRate = totalItems > 0 ? $"{Math.Round((double)successfulItems / totalItems * 100, 1)}%" : "0%";
        SelectedFilterStats = new HistoryStats(totalItems, successfulItems, totalBytes, totalAmount, successRate);
        LastUpdatedText = DateTime.Now.ToString("MMM d, HH:mm:ss", CultureInfo.InvariantCulture);

        HasNoRecords = TotalRecordCount == 0;
        HeaderSubtitle = $"{TotalRecordCount:N0} transfer(s) \u2022 {SelectedDateFilterString} \u2022 {SelectedStatusFilterString}";
        StatusMessage = $"Showing page {CurrentPage} of {TotalPages} ({Records.Count} records).";
    }

    [RelayCommand]
    private async Task NextPageAsync()
    {
        if (HasNextPage)
        {
            CurrentPage++;
            await LoadPageDataAsync();
        }
    }

    [RelayCommand]
    private async Task PreviousPageAsync()
    {
        if (HasPreviousPage)
        {
            CurrentPage--;
            await LoadPageDataAsync();
        }
    }

    [RelayCommand]
    private async Task FirstPageAsync()
    {
        if (CurrentPage > 1)
        {
            CurrentPage = 1;
            await LoadPageDataAsync();
        }
    }

    [RelayCommand]
    private async Task LastPageAsync()
    {
        if (CurrentPage < TotalPages)
        {
            CurrentPage = TotalPages;
            await LoadPageDataAsync();
        }
    }

    [RelayCommand]
    private void ClearFilters()
    {
        SearchQuery = string.Empty;
        SelectedStatusFilterString = StatusAll;
        SelectedDateFilterString = FilterAllTime;
    }

    [RelayCommand]
    private async Task ExportToCsvAsync()
    {
        if (TotalRecordCount == 0)
        {
            StatusMessage = "No records to export.";
            return;
        }

        string fileName = $"EasyCopier_History_Export_{DateTime.Now:yyyy_MM_dd}.csv";
        Dictionary<string, IList<string>> choices = new()
        {
            { "CSV File", new List<string> { ".csv" } }
        };

        string? filePath = await _filePickerService.PickSaveFileAsync(fileName, choices);

        if (filePath != null)
        {
            StatusMessage = "Exporting records...";
            (DateTime? startDate, DateTime? endDate, TransferStatus? statusFilter, string? search) = BuildQueryFilters();
            List<CopyHistoryRecord> allFiltered = await _copyHistoryService.GetAllFilteredRecordsAsync(startDate, endDate, statusFilter, search);
            bool success = await _reportService.ExportHistoryToCsvAsync(filePath, allFiltered);
            StatusMessage = success
                ? $"Exported {allFiltered.Count} record(s) to {Path.GetFileName(filePath)}"
                : "Export failed. Check logs.";
        }
    }
}
