# Security

JobPilot handles resumes, profile data, job descriptions and tailored exports.
Treat all of these as sensitive. The supported MVP does not control a browser,
submit applications, store platform cookies or expose platform passwords to the
web product.

## Supported Deployment

The current supported deployment is a private local installation. Do not expose
the API directly to the internet.

Production-like environments must provide:

- `Auth__JwtKey` with at least 32 characters;
- `Security__SecretKey` with at least 32 characters;
- a non-default bootstrap password or disabled bootstrap seed;
- explicit trusted frontend origins;
- TLS and a network access policy.

`Security__SecretKey` must be independent from `Auth__JwtKey`. Legacy data
encrypted with the JWT key may be read only during an explicitly configured
migration (`Security__AllowLegacyJwtDataProtection=true`) and should be
re-encrypted or removed before that flag is disabled.

Never commit secrets. Use environment variables or .NET user-secrets. Keep
`/api/automation` disabled; legacy agent code is quarantine-only
until a separate security review authorizes a new product surface. The browser
extension was removed entirely: there is no `chrome-extension://` CORS origin,
no pairing flow, and no `api/extension/*` route.

Uploads are private, capped at 10 MB, checked against basic file signatures and
stored with server-generated names. DOCX packages are bounded before parsing,
and temporary job-source files are removed after analysis. Before exposing the
product beyond a private local installation, add malware scanning, encrypted
storage and an explicit retention/deletion policy for resume files.

## Sessions

Access tokens last 1 hour and carry `NameIdentifier`. Renewal uses rotating
refresh tokens (`POST /api/auth/refresh`, 7 days); reuse of a rotated token is
rejected and `POST /api/auth/logout` revokes. Five invalid logins lock the
account for 15 minutes. To rotate server keys, change `Auth__JwtKey` and
`Security__SecretKey` (independent, 32+ chars) and restart: sessions and
data-protection payloads re-issue on next login. Legacy agent routes stay
behind the `LegacyRuntime` policy; user tokens only carry the
`legacy_runtime` claim when `Features:EnableLegacyRuntime=true`, which must
stay `false` in any shared environment.

External AI providers must use HTTPS and resolve to public addresses; hosts
outside the built-in allowlist (OpenAI, Anthropic, Kimi) need
`Ai:AllowedHosts` (`Ai__AllowedHosts` in env / compose). Prompts are capped,
redacted of PII and retried once on transient failure.
