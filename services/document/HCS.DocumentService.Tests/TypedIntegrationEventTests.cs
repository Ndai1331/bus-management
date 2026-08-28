using HCS.DocumentService.Integration;
using HCS.IntegrationEvents;
using HCS.IntegrationEvents.Documents;

namespace HCS.DocumentService.Tests;

public sealed class TypedIntegrationEventTests
{
    [Fact]
    public async Task Document_assigned_outbox_is_deserialized_and_delivered_as_canonical_type()
    {
        var projection = new AssignmentProjection();
        var bus = new ProjectionEventPublisher(projection);
        var publisher = new AbpOutboxEventPublisher(bus);
        var eventData = new DocumentAssignedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr-42",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "Approver");
        var message = OutboxFactory.CreateCanonical(eventData, eventData.CorrelationId!, DateTime.UtcNow);

        await publisher.PublishAsync(message, default);
        await publisher.PublishAsync(message, default);

        Assert.Equal(DocumentIntegrationEventNames.DocumentAssigned, message.EventName);
        Assert.Equal(1, projection.Count);
        Assert.Equal(eventData.DocumentId, projection.DocumentId);
        Assert.Equal(eventData.AssignmentId, projection.AssignmentId);
        Assert.Equal("corr-42", projection.CorrelationId);
        Assert.Equal(1, projection.SchemaVersion);
    }

    [Theory]
    [InlineData("workflow")]
    [InlineData("signed")]
    public async Task Document_events_use_stable_versioned_names(string eventKind)
    {
        var bus = new RecordingEventPublisher();
        var publisher = new AbpOutboxEventPublisher(bus);
        var eventData = eventKind == "workflow"
            ? (IntegrationEvent)new DocumentWorkflowChangedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr",
                Guid.NewGuid(), Guid.NewGuid(), "Completed")
            : new DocumentSignedEto(Guid.NewGuid(), DateTimeOffset.UtcNow, "corr", Guid.NewGuid(), Guid.NewGuid(),
                "in", "out", "remote-ca");
        var message = OutboxFactory.CreateCanonical(eventData, "corr", DateTime.UtcNow);

        await publisher.PublishAsync(message, default);

        Assert.Equal(eventKind == "workflow"
            ? DocumentIntegrationEventNames.WorkflowChanged
            : DocumentIntegrationEventNames.Signed, message.EventName);
        Assert.Equal(eventData.GetType(), bus.LastEventType);
    }

    private sealed class ProjectionEventPublisher(AssignmentProjection projection) : ITypedDistributedEventPublisher
    {
        public Task PublishAsync<T>(T eventData) where T : class
        {
            Assert.IsType<DocumentAssignedEto>(eventData);
            projection.Apply((DocumentAssignedEto)(object)eventData);
            return Task.CompletedTask;
        }
    }

    private sealed class AssignmentProjection
    {
        private readonly HashSet<Guid> _processed = [];
        public int Count => _processed.Count;
        public Guid DocumentId { get; private set; }
        public Guid AssignmentId { get; private set; }
        public string? CorrelationId { get; private set; }
        public int SchemaVersion { get; private set; }

        public void Apply(DocumentAssignedEto eventData)
        {
            if (!_processed.Add(eventData.EventId)) return;
            DocumentId = eventData.DocumentId;
            AssignmentId = eventData.AssignmentId;
            CorrelationId = eventData.CorrelationId;
            SchemaVersion = eventData.SchemaVersion;
        }
    }

    private sealed class RecordingEventPublisher : ITypedDistributedEventPublisher
    {
        public Type? LastEventType { get; private set; }

        public Task PublishAsync<T>(T eventData) where T : class
        {
            LastEventType = eventData.GetType();
            return Task.CompletedTask;
        }
    }
}
