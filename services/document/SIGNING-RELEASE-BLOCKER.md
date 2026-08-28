# Signing SDK release blocker

The local lab now exposes the VISNAM/Vin-HSM and TAG Remote CA adapters through `ISigningProviderFactory`. Do not package or deploy the VISNAM SDK references, native binaries, or derived code until the owner supplies written redistribution rights and a security review approves the integration. CI/release policy must keep this provider licensing decision as a blocking condition for production artifacts.
