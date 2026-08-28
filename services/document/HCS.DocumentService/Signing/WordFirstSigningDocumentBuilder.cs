using HCS.DocumentService.Workflows;

namespace HCS.DocumentService.Signing;

/// <summary>
/// Keeps the Word-first contract explicit: electronic signing replaces the
/// numbered signature image and text in DOCX, while VISNAM/TAG keep the numbered
/// image placeholder for the provider to render after DOCX-to-PDF conversion.
/// </summary>
internal static class WordFirstSigningDocumentBuilder
{
    public static byte[] Replace(byte[] sourceDocx, SigningKind kind, byte[] signatureImage,
        int stepOrder, string signerName, string note)
    {
        return kind == SigningKind.Electronic
            ? WordPlaceholderReplacer.ReplaceApproval(sourceDocx, stepOrder, signatureImage, signerName, note)
            : WordPlaceholderReplacer.ReplaceApprovalText(sourceDocx, stepOrder, signerName, note);
    }
}
