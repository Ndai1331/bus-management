using Microsoft.EntityFrameworkCore;

namespace HCS.DocumentService.Workflows;

internal static class WorkflowDefinitionStepReplacer
{
    public static async Task ReplaceAsync(DocumentServiceDbContext db, WorkflowDefinition definition,
        IReadOnlyList<WorkflowStepInput> steps, CancellationToken cancellationToken = default)
    {
        // Detach the parent before querying its children: while it is tracked, EF fixes the
        // loaded steps into Steps, so ReplaceSteps clears them and EF then deletes rows this
        // method already removed, which throws DbUpdateConcurrencyException.
        db.Entry(definition).State = EntityState.Detached;

        var obsolete = await db.WorkflowSteps.Where(x => x.DefinitionId == definition.Id).ToListAsync(cancellationToken);
        if (obsolete.Count > 0)
        {
            // Flush the deletes before inserting, so reused codes and orders never collide
            // with the unique (DefinitionId, Code) and (DefinitionId, Order) indexes.
            db.WorkflowSteps.RemoveRange(obsolete);
            await db.SaveChangesAsync(cancellationToken);
        }

        // Drop any in-memory children so Attach cannot re-track rows we just deleted.
        if (definition.Steps.Count > 0)
            definition.ReplaceSteps([]);

        db.WorkflowDefinitions.Attach(definition);
        db.Entry(definition).State = EntityState.Modified;

        try
        {
            definition.ReplaceSteps(steps);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("Workflow step code, name, and required permission cannot be empty.", ex);
        }

        // Replacement steps are always inserts, even when they reuse a previous code.
        foreach (var step in definition.Steps)
            db.Entry(step).State = EntityState.Added;
    }
}
