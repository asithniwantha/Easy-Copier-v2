using EasyCopier.Features.SmartAdder;
using EasyCopier.Features.SmartAdder.Models;
using EasyCopier.Shared.Platform;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EasyCopier.Tests.Features.SmartAdder;

public sealed class SmartAdderViewModelTests
{
    private readonly Mock<IWindowService> _windowServiceMock = new();
    private readonly Mock<ISmartAdderHistoryService> _historyServiceMock = new();

    private SmartAdderViewModel CreateViewModel() =>
        new(_windowServiceMock.Object, _historyServiceMock.Object, NullLogger<SmartAdderViewModel>.Instance);

    [Fact]
    public void EditingCells_MaintainsTrailingEmptyCellAndUpdatesTotalAndNegativeFlag()
    {
        SmartAdderViewModel vm = CreateViewModel();
        Assert.Single(vm.Cells);

        vm.Cells[0].InputValue = "100.5";
        Assert.Equal(2, vm.Cells.Count);
        Assert.Equal(100.5, vm.TotalSum, precision: 4);
        Assert.False(vm.Cells[0].IsNegative);

        vm.Cells[1].InputValue = "-25.5";
        Assert.Equal(3, vm.Cells.Count);
        Assert.True(vm.Cells[1].IsNegative);
        Assert.Equal(75.0, vm.TotalSum, precision: 4);

        // Clearing the second cell collapses extra trailing empty cells back to a single trailing blank row.
        vm.Cells[1].InputValue = string.Empty;
        Assert.Equal(2, vm.Cells.Count);
        Assert.Equal(100.5, vm.TotalSum, precision: 4);
    }

    [Fact]
    public void DeleteCell_UpdatesTotalAndLeavesSingleCellEmptyWhenLast()
    {
        SmartAdderViewModel vm = CreateViewModel();
        vm.Cells[0].InputValue = "40";
        vm.Cells[1].InputValue = "60";

        vm.DeleteCell(vm.Cells[0]);
        Assert.Equal(60, vm.TotalSum);
        Assert.Equal(2, vm.Cells.Count);

        vm.DeleteCell(vm.Cells[0]);
        Assert.Single(vm.Cells);
        Assert.Equal(string.Empty, vm.Cells[0].InputValue);
        Assert.Equal(0, vm.TotalSum);
    }

    [Fact]
    public void OpenHistoryCommand_DelegatesToWindowService()
    {
        SmartAdderViewModel vm = CreateViewModel();

        vm.OpenHistoryCommand.Execute(null);

        _windowServiceMock.Verify(w => w.ShowWindow(AppWindowKind.SmartAdderHistory, null, null), Times.Once);
    }

    [Fact]
    public async Task ClearAllCommand_LogsValidEntriesToHistoryAndResetsCells()
    {
        SmartAdderHistoryRecord? savedRecord = null;
        _historyServiceMock
            .Setup(h => h.AddRecordAsync(It.IsAny<SmartAdderHistoryRecord>()))
            .Callback<SmartAdderHistoryRecord>(r => savedRecord = r)
            .Returns(Task.CompletedTask);

        SmartAdderViewModel vm = CreateViewModel();
        vm.Cells[0].InputValue = "12.5";
        vm.Cells[1].InputValue = "7.5";

        await vm.ClearAllCommand.ExecuteAsync(null);

        Assert.NotNull(savedRecord);
        Assert.Equal("[12.5,7.5]", savedRecord!.EntriesJson);
        Assert.Equal(20.0, savedRecord.TotalSum, precision: 4);
        Assert.Single(vm.Cells);
        Assert.Equal(0, vm.TotalSum);
        Assert.Equal(string.Empty, vm.Cells[0].InputValue);
    }

    [Fact]
    public async Task SmartAdderHistoryViewModel_InitializeAsync_PopulatesEntriesAndEmptyState()
    {
        _historyServiceMock
            .Setup(h => h.GetRecentRecordsAsync(200))
            .ReturnsAsync(
            [
                new SmartAdderHistoryRecord
                {
                    Id = 1,
                    Timestamp = new DateTime(2026, 5, 1, 9, 30, 0),
                    EntriesJson = "[10,20]",
                    TotalSum = 30
                }
            ]);

        SmartAdderHistoryViewModel historyVm = new(_historyServiceMock.Object);
        await historyVm.InitializeAsync();

        Assert.False(historyVm.IsEmpty);
        Assert.Single(historyVm.Records);
        Assert.Equal([10d, 20d], historyVm.Records[0].Entries);
        Assert.Contains("30", historyVm.Records[0].TotalDisplay, StringComparison.Ordinal);
    }
}
