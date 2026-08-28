# HCS Web Gateway

License-clean ABP Community 10.6 / .NET 10 reverse-proxy host for the HCS microservices.

## BFF runtime contract

The Gateway is the only browser-facing OIDC client. It uses authorization code + PKCE, stores the authentication ticket (access token, refresh token, and claims) in Redis through `ITicketStore`, and keeps only a small session-id cookie in the browser. Saving those tokens in the cookie itself chunks `.HCS.Bff` until Kestrel returns **431 Request Header Fields Too Large** and Blazor WASM fails during module initialization. The Gateway refreshes expiring access tokens server-side and adds the current access token to proxied requests. Tokens are never returned to WebAssembly code.

Required environment variables/User Secrets:

```text
Authentication__Authority=https://localhost:44401
Authentication__ClientId=HCS_App
Authentication__ClientSecret=<secret>
DataProtection__Redis=localhost:6379,abortConnect=true
```

The OpenIddict `HCS_App` registration must be confidential and use:

```text
redirect_uri=https://localhost:44402/signin-oidc
post_logout_redirect_uri=https://localhost:44403
grants=authorization_code,refresh_token
```

The local flow does not force `prompt=login` on Gateway→AuthServer challenges, so a dropped BFF cookie can resume the AuthServer session instead of asking for a second password. Sign-out is local BFF only. Keycloak silent SSO and single logout are not implemented.

For production, Gateway and Blazor additionally require the same key-encryption certificate:

```text
DataProtection__Certificate__Path=/absolute/runtime/path/hcs-dp.pfx
DataProtection__Certificate__Password=<secret>
```

The PFX and password must come from a mounted secret/KMS integration and must not be tracked. Startup fails outside Development when this certificate is absent. Redis connection and sync timeouts default to five seconds and are capped at fifteen seconds through `DataProtection__ConnectTimeoutSeconds`.

## Same-host deployment

Local development uses `https://localhost:44402` for Gateway and `https://localhost:44403` for Blazor. Cookies are host-scoped and ports do not affect cookie matching.

Production should put both apps behind the same public host. Separate subdomains are accepted only when `Bff__CookieDomain` is an explicit parent domain of Gateway and every `App:CorsOrigins` entry; `localhost`, IP addresses, and top-level hostnames are rejected as cookie domains. Blazor must receive the identical `Bff__CookieDomain`, Redis connection, certificate, cookie name and Data Protection application name.

## Run locally

```bash
dotnet run --project HCS.WebGateway.csproj
```

The gateway listens on `https://localhost:44402`. A trusted ASP.NET Core development certificate is required. Liveness is available at `GET /health`.

Allowed browser origins are configured in `App:CorsOrigins`. Override them with environment-specific configuration when the Blazor origin changes.

## Routing and browser security

Routes in `ReverseProxy` forward Platform, Organization, Document, WorkManagement, and Collaboration traffic to ports `44411` through `44415`. The Collaboration cluster includes `/hubs/chat` for SignalR WebSocket and negotiation traffic.

All `/api/**` and `/hubs/**` routes require the BFF cookie. YARP replaces any browser-supplied authorization value with the saved access token; each microservice still validates that bearer token and enforces its own policies. Missing or failed refresh tokens cause local cookie sign-out and HTTP 401.

Unsafe proxy requests require the request token returned by `GET /bff/antiforgery` in `X-XSRF-TOKEN`. `BffHttpMessageHandler` includes credentials, obtains this token, retries once after token invalidation, and refuses absolute requests outside the configured Gateway origin. Use the same public handler as the `HttpMessageHandlerFactory` for SignalR so `/hubs/chat/negotiate` receives the same antiforgery behavior. WebSocket upgrades additionally require an `Origin` exactly matching `App:CorsOrigins`.

Available BFF endpoints are `/bff/login`, `/bff/logout`, `/bff/user`, and `/bff/antiforgery`. Protected BFF/API endpoints return 401 or 403 instead of HTML login redirects.

## Verify

```bash
dotnet test HCS.WebGateway.Tests/HCS.WebGateway.Tests.csproj
```

The tests validate routing, CORS, 401 behavior, refresh-token rotation concurrency, antiforgery policy, WebSocket origins, same-host cookie rules, open redirects, and the production certificate gate.
