# Domain boundaries and service names

Each domain owns one logical PostgreSQL database. APIs and workers within the same domain share that database. Services from another domain use authenticated APIs rather than database credentials.

| Domain / database | Compose services | Responsibility |
| --- | --- | --- |
| `membership` | `membership` | Identity, sessions, profiles, 2FA and durable identity-delivery outbox |
| `boards` | `boards`, `boards-worker` | Workspaces, workspace members, invitations, cards and scheduled Jira synchronization |
| `automations` | `automations`, `automations-worker` | Packages, execution records, installation/execution jobs and results |
| `ai` | `ai` | Personal model connections, settings, usage and conversation orchestration |
| No database | `portal`, `mcp-server`, `rabbitmq` | Interface/gateway, authenticated MCP read/action tools and durable message broker |

One PostgreSQL instance hosts the four separate logical databases. `membership` no longer has `ConnectionStrings__Boards`, a boards database context or boards credentials. Its own PostgreSQL role cannot connect to the boards database. The workers share their own domain's database; this is not cross-domain access.

## Identity projection delivery

Registration, external sign-in, profile and avatar changes record a public identity projection in `membership_directory_outbox`, in the same transaction as the identity change. The payload includes ID, name, email, avatar and creation date, never passwords, sessions or authenticator secrets.

After commit, membership attempts HTTP delivery to `boards`. A background dispatcher retries pending records. Per-user advisory locks serialize deliveries; a version-qualified delete prevents deleting a newer queued update. Failed delivery leaves the event in membership's database.

The internal boards endpoint `/internal/membership/identities/{id}` verifies an HMAC signature over timestamp, path and exact body, with a short freshness window. It upserts the public projection and creates the user's personal workspace if absent. Repeated identical deliveries do not duplicate the workspace or increment its version. Boards updates its own member projections and workspace versions. This endpoint is not forwarded by the public portal gateway.

The HTTP origin is configured through `DOMUS_BOARDS_URL`; both services receive `DOMUS_IDENTITY_SYNC_SECRET`. A production deployment should supply a private random secret of at least 32 characters. Startup rejects missing configuration.

Delivery is eventually consistent: identity updates can succeed while boards is unavailable, and the owning domain catches up through retry. A boards outage can temporarily delay creation of a new account's personal workspace. The fast post-commit attempt keeps normal local use immediate.

## Invitations

Public `/api/invitations/...` routes now go to `boards`. Invitation lookup, invited-email validation, acceptance and workspace version updates run in the domain that owns workspace membership. Session validity still comes from membership through the existing authenticated session-context API.

## Upgrade

Compose service names changed:

| Previous | Current |
| --- | --- |
| `work-api` | `boards` |
| `jira-worker` | `boards-worker` |
| `automation-api` | `automations` |
| `automation-worker` | `automations-worker` |
| `ai-api` | `ai` |

The worker publication component is now `automations-worker`, matching its service/domain name. Public browser URL prefixes, including `/ai-api/` and `/automations-api/`, stay compatible with existing clients and OAuth callbacks.

Rebuild the affected images, then run the single Compose file with `up -d --remove-orphans --wait`. Renaming does not rename or erase PostgreSQL or protection-key volumes. Orphan removal retires the old service containers so old workers cannot continue processing alongside their replacements. Existing membership databases gain the outbox table idempotently at startup.

## Checks

The integration suite verifies registration/2FA, profile propagation, invitation acceptance, workspace isolation, the absence of the foreign database connection, database-role separation and delivery after a simulated boards outage. Boards regression tests verify internal signature rejection, timestamp freshness, idempotent workspace creation and secret-free identity projections.

## Authentication policy

Business APIs require a validated Domus session and domain permissions. Boards and membership use a default authenticated fallback policy; branding reads and API documentation require a session too. AI/MCP and automation enforce authentication before dispatching non-bootstrap requests. Logout requires a session.

The identity-delivery endpoint authenticates the sending service through its timestamped HMAC signature. Automation worker archive reads require a bearer service credential, checked both at the internal route boundary and by the archive handler. A network address alone is not a service credential.

Health endpoints are restricted to loopback probes inside each service container, including the automation worker. They are not exposed through the portal as anonymous diagnostic APIs. Explicit bootstrap exceptions are registration/login, the minimal session/provider availability response, external-provider sign-in/callback handling, and local AI origin discovery. OAuth callbacks additionally validate their state/correlation and applicable session. These exceptions let users obtain authentication; they do not expose workspace data or business operations.

Before authentication the portal uses default branding. Protected branding is loaded after the session changes.
