# Document signing signature filtering

Status: completed

## Goal

In the approval modal on `/document-signing`, choose the signing method first and show only the current user's usable signatures for that method.

## Findings

- `DocumentSigning.razor` currently loads all user signatures once and renders them without filtering.
- The backend maps `SigningKind.Electronic` to `UserSignatureType.Electronic`; every other signing kind requires `UserSignatureType.Digital`.
- The backend rejects inactive, not-yet-valid, expired, or provider-mismatched signatures, so the modal should avoid offering those choices.

## Implementation

1. Add a method-aware signature projection in `DocumentSigning.razor`.
2. Render the signing method selector before the signature selector and handle method changes explicitly.
3. Reset the selected signature to the first matching default (or empty) when opening the modal and whenever the method changes.
4. Keep the existing signing request contract unchanged; backend validation remains authoritative.

## Verification

- Run `dotnet build src/HCS.Blazor/HCS.Blazor.csproj --no-restore`.
- Run `git diff --check`.
- Inspect the diff to ensure the existing unrelated dirty changes remain untouched.

## Result

- The modal now selects the signing method before the signature.
- Electronic signing shows electronic signatures; Remote CA/HSM/USB token show active, valid digital signatures matching the selected provider and capability flag.
- Changing method resets the selection to the matching default/first signature; approval is guarded when no valid signature exists.
- Signature/credential load failures expose a retry action instead of looking like an empty list.
- Blazor build passed with 0 warnings and 0 errors; full solution tests passed.
