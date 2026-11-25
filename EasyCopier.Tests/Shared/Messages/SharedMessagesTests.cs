using CommunityToolkit.Mvvm.Messaging;
using EasyCopier.Shared.Messages;
using Xunit;

namespace EasyCopier.Tests.Shared.Messages;

public sealed class SharedMessagesTests
{
    [Fact]
    public void CrossSliceMessages_AreImmutableAndValueEqual()
    {
        Assert.True(typeof(TransferSourceItem).IsValueType);

        Guid batchId = Guid.NewGuid();
        var msg1 = new TransferBatchCompletedMessage(batchId, "E:", 2, 0, 1024L, TimeSpan.FromSeconds(5));
        var msg2 = new TransferBatchCompletedMessage(batchId, "E:", 2, 0, 1024L, TimeSpan.FromSeconds(5));
        Assert.Equal(msg1, msg2);

        var drive1 = new DriveConnectionChangedMessage("E:", true, 1000, 2000);
        var drive2 = new DriveConnectionChangedMessage("E:", true, 1000, 2000);
        Assert.Equal(drive1, drive2);
    }

    [Fact]
    public void Messenger_DeliversAllSharedMessagesHeadlessly()
    {
        var messenger = new WeakReferenceMessenger();
        var receiver = new TestRecipient();
        messenger.RegisterAll(receiver);

        var item = new TransferSourceItem("Game A", @"D:\Games\Game A", 4096, 100m, false);
        messenger.Send(new EnqueueTransferRequestMessage([item], "E:"));
        messenger.Send(new TransferBatchCompletedMessage(Guid.NewGuid(), "E:", 1, 0, 4096, TimeSpan.FromSeconds(2)));
        messenger.Send(new DriveConnectionChangedMessage("E:", true, 5000, 10000));
        messenger.Send(new SettingsChangedMessage(SourcePathsUpdated: true, PriceTiersUpdated: true));

        Assert.NotNull(receiver.LastEnqueue);
        Assert.Single(receiver.LastEnqueue.Items);
        Assert.NotNull(receiver.LastCompleted);
        Assert.Equal("E:", receiver.LastCompleted.TargetDriveLetter);
        Assert.NotNull(receiver.LastDrive);
        Assert.True(receiver.LastDrive.IsConnected);
        Assert.NotNull(receiver.LastSettings);
        Assert.True(receiver.LastSettings.SourcePathsUpdated);
        Assert.True(receiver.LastSettings.PriceTiersUpdated);
    }

    internal sealed class TestRecipient :
        IRecipient<EnqueueTransferRequestMessage>,
        IRecipient<TransferBatchCompletedMessage>,
        IRecipient<DriveConnectionChangedMessage>,
        IRecipient<SettingsChangedMessage>
    {
        public EnqueueTransferRequestMessage? LastEnqueue { get; private set; }
        public TransferBatchCompletedMessage? LastCompleted { get; private set; }
        public DriveConnectionChangedMessage? LastDrive { get; private set; }
        public SettingsChangedMessage? LastSettings { get; private set; }

        public void Receive(EnqueueTransferRequestMessage message) => LastEnqueue = message;
        public void Receive(TransferBatchCompletedMessage message) => LastCompleted = message;
        public void Receive(DriveConnectionChangedMessage message) => LastDrive = message;
        public void Receive(SettingsChangedMessage message) => LastSettings = message;
    }
}
