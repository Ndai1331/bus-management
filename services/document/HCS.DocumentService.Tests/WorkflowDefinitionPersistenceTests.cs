using HCS.DocumentService.Workflows;
using Microsoft.EntityFrameworkCore;

namespace HCS.DocumentService.Tests;

public sealed class WorkflowDefinitionPersistenceTests
{
    private static readonly DateTime Now = new(2026, 8, 21, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Replace_without_tracking_old_steps_saves_without_concurrency_conflict()
    {
        await using var db = CreateDb();
        var id = await SeedDefinitionAsync(db);

        var definition = await db.WorkflowDefinitions.SingleAsync(x => x.Id == id);
        definition.Rename("Updated");
        await WorkflowDefinitionStepReplacer.ReplaceAsync(db, definition,
        [
            new("review", "Review", 1, "Documents.Review"),
            new("sign", "Sign", 2, "Documents.Approve", "SIGN")
        ]);
        Assert.Equal([EntityState.Added, EntityState.Added],
            db.ChangeTracker.Entries<WorkflowStep>().Select(entry => entry.State).ToArray());
        await db.SaveChangesAsync();

        var saved = await db.WorkflowDefinitions.AsNoTracking().Include(x => x.Steps)
            .SingleAsync(x => x.Id == id);
        Assert.Equal("Updated", saved.Name);
        Assert.Equal(["review", "sign"], saved.Steps.OrderBy(x => x.Order).Select(x => x.Code).ToArray());
    }

    [Fact]
    public async Task Replace_keeps_unique_codes_when_reusing_the_first_step_code()
    {
        await using var db = CreateDb();
        var id = await SeedDefinitionAsync(db);

        var definition = await db.WorkflowDefinitions.SingleAsync(x => x.Id == id);
        await WorkflowDefinitionStepReplacer.ReplaceAsync(db, definition,
        [
            new("review", "Review updated", 1, "Documents.Review"),
            new("approve", "Approve", 2, "Documents.Approve")
        ]);
        await db.SaveChangesAsync();

        var saved = await db.WorkflowSteps.AsNoTracking().Where(x => x.DefinitionId == id)
            .OrderBy(x => x.Order).ToListAsync();
        Assert.Equal(["review", "approve"], saved.Select(x => x.Code).ToArray());
        Assert.Equal("Review updated", saved[0].Name);
    }

    private static DocumentServiceDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<DocumentServiceDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new DocumentServiceDbContext(options);
        db.Database.EnsureCreated();
        return db;
    }

    private static async Task<Guid> SeedDefinitionAsync(DocumentServiceDbContext db)
    {
        var definition = new WorkflowDefinition(Guid.NewGuid(), "wf", "Workflow",
            [new WorkflowStepInput("review", "Review", 1, "Documents.Review")], Now);
        db.WorkflowDefinitions.Add(definition);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return definition.Id;
    }
}
