using HCS.DocumentService.Workflows;
using System.Collections.Generic;

namespace HCS.DocumentService.Tests;

public sealed class WorkflowTests
{
    private static readonly DateTime Now = new(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Definition_allows_empty_steps_for_draft()
    {
        var definition = new WorkflowDefinition(Guid.NewGuid(), "draft", "Draft", [], Now);
        Assert.Empty(definition.Steps);
        definition.ReplaceSteps([]);
        Assert.Empty(definition.Steps);
        Assert.Equal(WorkflowSignModes.Sequential, definition.SignMode);
        Assert.Throws<InvalidOperationException>(() => definition.EnsureStartable());
        Assert.Throws<InvalidOperationException>(() =>
            new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), definition, "start-empty", Now));
    }

    [Fact]
    public void Definition_with_only_view_steps_cannot_start()
    {
        var definition = new WorkflowDefinition(Guid.NewGuid(), "view-only", "View only", new[]
        {
            new WorkflowStepInput("view", "View", 1, "Documents.View", "VIEW")
        }, Now);

        Assert.DoesNotContain(definition.Steps, step => step.IsBlocking);
        Assert.Throws<InvalidOperationException>(() => definition.EnsureStartable());
        Assert.Throws<InvalidOperationException>(() =>
            new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), definition, "start-view-only", Now));
    }

    [Fact]
    public void Definition_rejects_duplicate_steps()
    {
        var steps = new[] { new WorkflowStepInput("review", "Review", 1, "Documents.Review"), new WorkflowStepInput("review", "Again", 2, "Documents.Approve") };
        Assert.Throws<InvalidOperationException>(() => new WorkflowDefinition(Guid.NewGuid(), "standard", "Standard", steps, Now));
    }

    [Fact]
    public void Decision_is_idempotent_and_completes_in_order()
    {
        var definition = new WorkflowDefinition(Guid.NewGuid(), "standard", "Standard", new[]
        {
            new WorkflowStepInput("review", "Review", 1, "Documents.Review"),
            new WorkflowStepInput("approve", "Approve", 2, "Documents.Approve")
        }, Now);
        var instance = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), definition, "start-1", Now);
        var steps = definition.Steps.OrderBy(x => x.Order).ToList();
        var firstTask = instance.Tasks.Single();
        Assert.True(instance.Decide(firstTask.Id, true, Guid.NewGuid(), null, "decision-1", steps, Now));
        Assert.False(instance.Decide(firstTask.Id, true, Guid.NewGuid(), null, "decision-1", steps, Now));
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        var secondTask = instance.Tasks.Single(x => x.Status == ApprovalTaskStatus.Pending);
        Assert.True(instance.Decide(secondTask.Id, true, Guid.NewGuid(), null, "decision-2", steps, Now));
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
    }

    [Fact]
    public void Replace_steps_keeps_unique_codes_and_orders()
    {
        var definition = new WorkflowDefinition(Guid.NewGuid(), "standard", "Standard", new[]
        {
            new WorkflowStepInput("review", "Review", 1, "Documents.Review")
        }, Now);
        definition.Rename("Updated");
        definition.ReplaceSteps(new[]
        {
            new WorkflowStepInput("review", "Review", 1, "Documents.Review"),
            new WorkflowStepInput("approve", "Approve", 2, "Documents.Approve")
        });
        Assert.Equal("Updated", definition.Name);
        Assert.Equal(2, definition.Steps.Count);
        definition.ReplaceSteps(new[]
        {
            new WorkflowStepInput("review", "Review", 1, "  ")
        });
        Assert.Equal("Documents.Workflow.Decide", definition.Steps.Single().RequiredPermission);
        Assert.Throws<InvalidOperationException>(() => definition.ReplaceSteps(new[]
        {
            new WorkflowStepInput("same", "A", 1, "Documents.Review"),
            new WorkflowStepInput("same", "B", 2, "Documents.Approve")
        }));
    }

    [Fact]
    public void Template_can_be_deactivated()
    {
        var template = new WorkflowTemplate(Guid.NewGuid(), "incoming", "Incoming", Guid.NewGuid(), 1, "{}", Now);
        template.SetActive(false);
        Assert.False(template.IsActive);
    }

    [Fact]
    public void Template_requires_valid_json_and_positive_version()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() => new WorkflowTemplate(Guid.NewGuid(), "incoming", "Incoming", Guid.NewGuid(), 1, "not-json", Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => new WorkflowTemplate(Guid.NewGuid(), "incoming", "Incoming", Guid.NewGuid(), 0, "{}", Now));
        var template = new WorkflowTemplate(Guid.NewGuid(), "incoming", "Incoming", Guid.NewGuid(), 1, "{}", Now);
        Assert.True(template.IsActive);
    }

    [Fact]
    public void Sign_step_copies_assignee_onto_approval_task()
    {
        var assignee = Guid.NewGuid();
        var definition = new WorkflowDefinition(Guid.NewGuid(), "sign", "Sign", new[]
        {
            new WorkflowStepInput("sign", "Sign", 1, "Documents.Approve", "SIGN", assignee)
        }, Now);
        var step = definition.Steps.Single();
        Assert.Equal(WorkflowStepTypes.Sign, step.Type);
        Assert.Equal(assignee, step.AssigneeUserId);
        var instance = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), definition, "start-sign", Now);
        Assert.Equal(assignee, instance.Tasks.Single().AssigneeUserId);
    }

    [Fact]
    public void Step_type_must_be_known()
    {
        Assert.Equal(WorkflowStepTypes.Process, WorkflowStepTypes.Normalize(null));
        Assert.Throws<ArgumentException>(() => WorkflowStepTypes.Normalize("FOO"));
        Assert.Throws<ArgumentException>(() => new WorkflowDefinition(Guid.NewGuid(), "bad", "Bad", new[]
        {
            new WorkflowStepInput("x", "X", 1, "Documents.Approve", "UNKNOWN")
        }, Now));
    }

    [Fact]
    public void Template_attaches_pdf_and_word_files()
    {
        var template = new WorkflowTemplate(Guid.NewGuid(), "incoming", "Incoming", Guid.NewGuid(), 1, "{}", Now);
        var pdfId = Guid.NewGuid();
        var wordId = Guid.NewGuid();
        template.AttachPdf(pdfId, "form.pdf", "application/pdf", "wf/pdf");
        template.AttachWord(wordId, "form.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document", "wf/word");
        Assert.Equal(pdfId, template.PdfFileId);
        Assert.Equal("form.pdf", template.PdfFileName);
        Assert.Equal(wordId, template.WordFileId);
        Assert.Equal("form.docx", template.WordFileName);
        template.UpdateContent("Incoming v2", """{"title":"form"}""", "PDF");
        Assert.Equal("Incoming v2", template.Name);
        Assert.Equal("PDF", template.OutputFormat);
        Assert.Contains("title", template.TemplateJson);
    }

    [Fact]
    public void Start_request_requires_exactly_one_document_source()
    {
        var workflowId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        Assert.True(WorkflowStartRequestRules.HasExactlyOneSource(
            new StartWorkflowRequest(null, workflowId, "workflow-template", UseWorkflowTemplateFile: true)));
        Assert.True(WorkflowStartRequestRules.HasExactlyOneSource(
            new StartWorkflowRequest(documentId, workflowId, "existing-document")));
        Assert.False(WorkflowStartRequestRules.HasExactlyOneSource(
            new StartWorkflowRequest(null, workflowId, "no-source")));
        Assert.False(WorkflowStartRequestRules.HasExactlyOneSource(
            new StartWorkflowRequest(documentId, workflowId, "two-sources", UseWorkflowTemplateFile: true)));
    }

    [Fact]
    public void Completed_single_step_workflow_accepts_same_decision_retry()
    {
        var definition = new WorkflowDefinition(Guid.NewGuid(), "one", "One", new[]
            { new WorkflowStepInput("approve", "Approve", 1, "Documents.Approve") }, Now);
        var instance = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), definition, "start-one", Now);
        var task = instance.Tasks.Single();
        var actor = Guid.NewGuid();
        Assert.True(instance.Decide(task.Id, true, actor, null, "same-command", definition.Steps.ToList(), Now));
        Assert.Equal(WorkflowInstanceStatus.Completed, instance.Status);
        Assert.False(instance.Decide(task.Id, true, actor, null, "same-command", definition.Steps.ToList(), Now));
    }

    [Fact]
    public void View_steps_are_skipped_when_starting()
    {
        var definition = new WorkflowDefinition(Guid.NewGuid(), "mix", "Mix", new[]
        {
            new WorkflowStepInput("see", "See", 1, "Documents.View", "VIEW"),
            new WorkflowStepInput("sign", "Sign", 2, "Documents.Approve", "SIGN")
        }, Now);
        var instance = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), definition, "start-view", Now);
        Assert.Equal(1, instance.CurrentStep);
        Assert.Equal("sign", instance.Tasks.Single().StepCode);
    }

    [Fact]
    public void Start_uses_signer_override()
    {
        var configured = Guid.NewGuid();
        var selected = Guid.NewGuid();
        var definition = new WorkflowDefinition(Guid.NewGuid(), "sign", "Sign", new[]
        {
            new WorkflowStepInput("sign", "Sign", 1, "Documents.Approve", "SIGN", configured)
        }, Now);
        var instance = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), definition, "start-override", Now,
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["sign"] = selected });
        Assert.Equal(selected, instance.Tasks.Single().AssigneeUserId);
    }

    [Fact]
    public void Role_step_uses_resolved_override()
    {
        var roleId = Guid.NewGuid();
        var resolved = Guid.NewGuid();
        var definition = new WorkflowDefinition(Guid.NewGuid(), "role", "Role", new[]
        {
            new WorkflowStepInput("review", "Review", 1, "Documents.Review", "PROCESS", null,
                WorkflowStepAssigneeTypes.RoleInSubmitterOu, roleId)
        }, Now);
        var instance = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), definition, "start-role", Now,
            new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase) { ["review"] = resolved });
        Assert.Equal(resolved, instance.Tasks.Single().AssigneeUserId);
    }

    [Fact]
    public void Return_and_resubmit_restore_running_state()
    {
        var definition = new WorkflowDefinition(Guid.NewGuid(), "ret", "Return", new[]
        {
            new WorkflowStepInput("review", "Review", 1, "Documents.Review", "PROCESS", null,
                WorkflowStepAssigneeTypes.SpecificUser, null, null, null, 2, true)
        }, Now);
        var instance = new WorkflowInstance(Guid.NewGuid(), Guid.NewGuid(), definition, "start-return", Now);
        var task = instance.Tasks.Single();
        Assert.True(instance.Decide(task.Id, false, Guid.NewGuid(), "send back", "return-1", definition.Steps.ToList(), Now, returnStep: true));
        Assert.Equal(WorkflowInstanceStatus.Returned, instance.Status);
        instance.Resubmit(definition.Steps.ToList(), Now, "resubmit-1");
        Assert.Equal(WorkflowInstanceStatus.Running, instance.Status);
        Assert.Equal(2, instance.Tasks.Count);
        Assert.Equal(ApprovalTaskStatus.Pending, instance.Tasks.Last().Status);
        Assert.NotNull(instance.Tasks.Last().DueAt);
    }

    [Fact]
    public void Kind_can_be_updated()
    {
        var kind = new WorkflowKind(Guid.NewGuid(), "cv", "Cong van", "desc", true, Now);
        kind.Update("Official", null, false);
        Assert.Equal("Official", kind.Name);
        Assert.False(kind.IsActive);
    }
}
