# Cấu hình provider ký số trong free license

## Provider mapping

| Provider code | Signing kind | Runtime flow | Required credential/assets |
|---|---|---|---|
| `VISNAM` | `HSM`, `UsbToken` | `BnnSoftSigns.SignText` + Vin-HSM SDK, PDF hash SHA-256 | TokenRef/API key, secret, `SealImg`, `LayoutImg` |
| `TAG` | `RemoteCa` | REST `/api/v2/pdf/sign/originaldata` + HMAC-SHA256 (`SignTextV2`) | TokenRef/API key, Base64 secret; seal/layout are optional |

Provider codes are normalized case-insensitively. The aliases `VINHSM`, `VIN-HSM`, `VIN_HSM`, `REMOTE_CA`, `REMOTE-CA`, and `REMOTECA` normalize to `VISNAM` or `TAG` respectively.

## Defaults

- `VISNAM` defaults to `https://sign-hn10.vin-hsm.com`, matching the licensed source. It is still overrideable through `Signing:Providers:VISNAM:DefaultEndpoint`.
- `TAG` has no hard-coded production endpoint. Set `Signing:Providers:TAG:DefaultEndpoint` or enter the deployment-specific base URL in the provider settings screen. Do not append `/api/v2/pdf/sign/originaldata`.
- Default timeout is 30 seconds; signature size defaults to 150 x 70. TAG requests are clamped to 30–240 seconds.
- `Signing:AllowedEndpointHosts` remains an explicit SSRF allowlist. Add the TAG host before saving or invoking a TAG credential.

## Keys and secrets

`TokenRef` and provider secrets are user/provider credentials, not source assets. They are accepted through the secured API/UI, encrypted with ASP.NET Core Data Protection at rest, masked in DTOs, and omitted from logs. The licensed source's ABP license, MinIO password, and OIDC client values are unrelated infrastructure secrets and are not migrated.

## Signed layout assets

The free service embeds the four VISNAM/Bnn assets (`chukytuoi.png`, `condau.png`, `layout.png`, `layout2.png`) and the two electronic-signature assets (`electronic-signature-layout.png`, `electronic-signature-layout1.png`). Electronic signing uses the configured layout when present; otherwise it composes the standard `Đã ký` badge from the embedded primary layout.

## SignXX whiteout safety

Electronic, TAG, and workflow text replacement now share the same PdfPig locator. It maps each text character back to its originating glyph/run and rejects non-finite, out-of-page, or page-sized bounding boxes before drawing a whiteout rectangle. This prevents a malformed/multi-character glyph mapping from covering unrelated columns or the whole page.
