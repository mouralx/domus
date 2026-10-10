# 001 — One portal, independent services

Status: implemented for manual automation execution.

## Decision

The React portal combines boards and automations while keeping three independent .NET 10 APIs: membership, boards and automations. Two separate workers handle external boards synchronization and automation installation/execution. Each component builds from its own Dockerfile under `infra/containers`, using the repository root as build context. A single `infra/compose.yaml` defines deployment.

One PostgreSQL 18 server hosts `membership`, `boards` and `automations`, with separate application roles and databases. Membership owns accounts, passwords, sessions, external identities, authenticator secrets and recovery codes. Boards retains public user projections for relational references, workspace membership, invitations, projects, cards and Jira configuration. Membership uses the boards connection for workspace provisioning, invitation acceptance and projection updates. This preserves existing relational behavior while removing authentication authority from boards. Membership currently reuses the boards authentication implementation through a project reference; separating that shared library further is possible without changing public routes.

The gateway sends `/api/auth`, `/api/invitations` and `/api/membership` to membership; existing boards routes remain under `/api`. Automation routes remain under `/automations-api/api`. Only the portal exposes a host port. Shared data-protection keys preserve the cookie format and encrypted 2FA/Jira secrets. Boards revalidates sessions against membership on every authenticated request, including pending photo/2FA flags. Automation workspace requests validate permissions via boards, which in turn validates the membership session. Revocation therefore takes effect without waiting for cookie expiry.

GitHub repository and encrypted PAT are platform configuration. Only configured platform administrators can read configuration metadata, save or remove it. These routes validate identity directly through membership and ignore workspace identifiers. Workspace owners manage automation installation/removal; members execute and inspect their workspace's results. A single legacy GitHub configuration is automatically re-encrypted with the platform purpose while retaining its encryption keys. Conflicting legacy settings are retained for manual resolution rather than choosing credentials arbitrarily.

Automation API and worker share package/input/result files. PAT keys are mounted only in the API. Completed execution and review requirement are distinct concerns. ZIP downloads, logs, uploads and runs enforce workspace boundaries. Installation and execution remain queued worker operations.

## Compatibility and validation

Public portal URLs, board routes, authentication routes and worker behavior remain stable. Original project namespaces are retained during the repository move. Installation uses one Compose file; tests exercise that same file in isolated projects and volumes. Existing two-server installations require logical dumps, identity extraction and restore into the three databases; see [database migration](../database-migration.md). Keys and shared files must be preserved. PostgreSQL 17 physical data must never be mounted into PostgreSQL 18.

Future card-triggered automation rules and direct run/card associations are outside this change.
