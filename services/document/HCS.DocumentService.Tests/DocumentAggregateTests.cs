using HCS.DocumentService.Documents;

namespace HCS.DocumentService.Tests;

public sealed class DocumentAggregateTests
{
    private static readonly DateTime Now = new(2026, 8, 3, 0, 0, 0, DateTimeKind.Utc);
    private static DocumentAggregate Create() => new(Guid.NewGuid(), "CV-001", "Công văn", null, Guid.NewGuid(), Now);

    [Fact]
    public void Classification_is_stored_until_the_document_is_immutable()
    {
        var document = Create();
        var typeId = Guid.NewGuid();
        document.Classify(typeId, null, null, null, null, Now);
        Assert.Equal(typeId, document.DocumentTypeId);
        document.AddFile(Guid.NewGuid(), "a.pdf", "application/pdf", 10, new string('a', 64), "documents/a", null, Now);
        document.Submit(null, Now);
        document.StartReview(null, Now);
        document.CompleteReview(true, null, null, Now);
        Assert.Throws<InvalidOperationException>(() => document.Classify(Guid.NewGuid(), null, null, null, null, Now));
    }

    [Fact]
    public void Submission_requires_at_least_one_file()
    {
        var document = Create();
        var error = Assert.Throws<InvalidOperationException>(() => document.Submit(Guid.NewGuid(), Now));
        Assert.Contains("at least one file", error.Message);
    }

    [Fact]
    public void Duplicate_content_is_rejected_and_approved_document_is_immutable()
    {
        var document = Create();
        var hash = new string('a', 64);
        document.AddFile(Guid.NewGuid(), "a.pdf", "application/pdf", 10, hash, "documents/a", null, Now);
        Assert.Throws<InvalidOperationException>(() => document.AddFile(Guid.NewGuid(), "copy.pdf", "application/pdf", 10, hash, "documents/b", null, Now));
        document.Submit(null, Now);
        document.StartReview(null, Now);
        document.CompleteReview(true, null, null, Now);
        Assert.Throws<InvalidOperationException>(() => document.Update("Changed", null, null, Now));
    }

    [Fact]
    public void Assignment_is_idempotent_for_same_user_and_responsibility()
    {
        var document = Create();
        var user = Guid.NewGuid();
        document.Assign(Guid.NewGuid(), user, "approver", null, Now);
        document.Assign(Guid.NewGuid(), user, "approver", null, Now);
        Assert.Single(document.Assignments);
    }

    [Fact]
    public void File_deletion_is_retriable_in_two_phases()
    {
        var document = Create();
        var file = document.AddFile(Guid.NewGuid(), "a.pdf", "application/pdf", 10, new string('a', 64), "documents/a", null, Now);
        Assert.Same(file, document.BeginFileDeletion(file.Id, null, Now));
        Assert.Same(file, document.BeginFileDeletion(file.Id, null, Now));
        Assert.True(file.IsPendingDeletion);
        document.CompleteFileDeletion(file.Id, null, Now);
        Assert.Empty(document.Files);
    }

    [Fact]
    public void Send_creates_view_assignment_and_revoke_hides_inbox()
    {
        var document = Create();
        var from = Guid.NewGuid();
        var to = Guid.NewGuid();
        document.Send(to, null, from, Now);
        Assert.Equal(from, document.FromUserId);
        Assert.Contains(document.Assignments, a => a.AssigneeUserId == to && a.Responsibility == "VIEW" && a.IsCurrent);
        document.RevokeInbox(from, Now);
        Assert.All(document.Assignments.Where(a => a.Responsibility == "VIEW"), a => Assert.False(a.IsCurrent));
    }

    [Fact]
    public void Duplicate_as_workflow_keeps_parent_and_classification()
    {
        var document = Create();
        var typeId = Guid.NewGuid();
        document.Classify(typeId, null, null, null, null, Now);
        var copy = document.DuplicateAsWorkflow(Guid.NewGuid(), "CV-001-WF", null, Now);
        Assert.Equal(DocumentSourceType.Workflow, copy.SourceType);
        Assert.Equal(document.Id, copy.ParentDocumentId);
        Assert.Equal(typeId, copy.DocumentTypeId);
        Assert.NotEqual(document.Id, copy.Id);
    }

    [Fact]
    public void Workflow_submitter_is_stored_without_overwriting_recipient_department()
    {
        var document = Create().DuplicateAsWorkflow(Guid.NewGuid(), "CV-001-WF", null, Now);
        var submitter = Guid.NewGuid();

        document.SetWorkflowSubmitter(submitter);

        Assert.Equal(submitter, document.FromUserId);
        Assert.Null(document.OrganizationUnitId);
    }

    [Fact]
    public void Generated_document_number_is_date_prefixed_and_compact()
    {
        var number = DocumentAppService.GenerateNumber(Now);

        Assert.Matches("^VB-20260803-[A-F0-9]{8}$", number);
    }

    [Fact]
    public void Reviewer_can_be_assigned_while_the_document_is_in_review()
    {
        var document = Create();
        document.AddFile(Guid.NewGuid(), "a.pdf", "application/pdf", 10, new string('a', 64), "documents/a", null, Now);
        document.Submit(null, Now);
        document.StartReview(null, Now);
        var signer = Guid.NewGuid();
        document.Assign(Guid.NewGuid(), signer, "sign", null, Now, "sign");
        Assert.Contains(document.Assignments, a => a.AssigneeUserId == signer && a.StepCode == "sign");
    }

    [Fact]
    public void Review_start_keeps_optional_signing_content_in_history()
    {
        var document = Create();
        document.AddFile(Guid.NewGuid(), "a.pdf", "application/pdf", 10, new string('a', 64), "documents/a", null, Now);
        document.Submit(null, Now);

        document.StartReview(null, Now, "Nội dung trình ký");

        Assert.Equal("Nội dung trình ký", document.History.Single(x => x.Action == "ReviewStarted").Detail);
    }

    [Fact]
    public void Word_and_pdf_files_can_be_paired()
    {
        var document = Create();
        var word = document.AddFile(Guid.NewGuid(), "a.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document", 12, new string('b', 64), "documents/a.docx", null, Now);
        var pdf = document.AddFile(Guid.NewGuid(), "a.pdf", "application/pdf", 20, new string('c', 64), "documents/a.pdf", null, Now);
        word.SetPairedFileId(pdf.Id);
        pdf.SetPairedFileId(word.Id);
        Assert.Equal(pdf.Id, word.PairedFileId);
        Assert.Equal(word.Id, pdf.PairedFileId);
    }
}
