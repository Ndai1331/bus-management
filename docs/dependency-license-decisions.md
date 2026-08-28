# Dependency license decisions

## Release blockers

### Blazorise 2.2 / Blazorise.Licensing 1.3

LeptonX Lite 5.6 and the current Blazor UI resolve `Blazorise.Licensing` from nuget.org. The package uses the Blazorise Software License Agreement rather than an OSI-approved open-source license. Blazorise states that organizations, including government entities, require a commercial license.

Development may continue so the requested LeptonX Lite UI can be migrated, but a production release is blocked until the deploying organization records proof of an applicable Blazorise commercial license. If that approval cannot be obtained, replace LeptonX Lite/Blazorise with an approved OSS UI stack before release.

This exception does not permit any ABP Commercial/Pro package. `scripts/audit-license-clean.sh` continues to reject those packages.

### Signing provider runtime (2026-08-24)

The free document service now intentionally references `Bnn.SignLib` 1.2.5 and `Bnn.Sdk` 1.1.3, plus PdfPig/PDFsharp/ImageSharp, because the requested parity includes the existing HSM, USB/token, and Remote CA provider flows. The provider helper source is vendored under the free DocumentService so Docker builds do not depend on the sibling licensed tree. This is a provider-runtime exception only: no ABP Commercial/Pro package is introduced, and the provider redistribution terms must be verified before production distribution.
