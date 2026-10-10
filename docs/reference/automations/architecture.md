> Referência do módulo de origem. Para executar a plataforma integrada Domus, consulte o [README principal](../../../README.md). O único Compose está em `infra/compose.yaml`.

# Architecture and execution

The browser calls the API through Nginx. The API stores metadata in PostgreSQL and
publishes durable jobs to RabbitMQ. The worker installs and executes automations.
API and worker share `/data`; the worker does not mount the API's `/keys` volume.
Only the portal exposes a host port. See [Deployment](deployment.md) for services,
networks and volume details.

## Installation

The repository and optional PAT are configured per account in **Definições**.
The API reads GitHub releases and returns compatible catalog entries. Installation
requests create database jobs and outbox entries; RabbitMQ carries
`installation:<job-id>` to the worker. The worker downloads through the API's
internal authenticated proxy, validates the archive and activates it on `/data`.
A shared private worker token protects that endpoint. GitHub credentials stay in
the API. Installation states are `queued`, `installing`, `completed` and `failed`.

## Execution

1. The API stores package and upload metadata in PostgreSQL and their files on
   the shared volume. Packages must match the **worker's** OS and architecture.
2. **Executar** validates inputs, writes private temporary input values on the
   volume, and saves an execution plus an outbox entry in one database transaction.
3. The API's outbox publisher sends only the execution ID to RabbitMQ using
   publisher confirms. Publication failures are retried; secrets/files are not
   included in queue messages.
4. The worker consumes execution and installation jobs one at a time and atomically claims its database row
   before launching the executable. It copies uploads into a fresh workspace and
   updates status/logs in PostgreSQL. Original uploads remain unchanged.
5. The API reads status from PostgreSQL and serves results as one ZIP, preserving
   their folder structure. Individual file routes remain available for compatibility.
   Downloads read from the shared
   volume after checking account ownership. The browser never talks to the worker.

Queue messages remain unacknowledged until terminal status is saved. Duplicate
messages for claimed/completed executions are acknowledged without rerunning the
package. API restarts preserve sessions and do not interrupt worker execution.
Worker restarts mark interrupted installations as failed for explicit retry.
Worker restarts mark interrupted executions as failed and clean up their temporary
credentials rather than retrying actions that might publish comments or create
paid requests. Jobs still queued remain available for the worker.

Cancellation is stored in PostgreSQL and the worker terminates the process tree.
The default execution limit is two hours; RabbitMQ's consumer acknowledgement
timeout is three hours. Log output redacts supplied environment values, and working
`.env`/temporary input files are deleted after completion, failure or cancellation.

Use one API and one worker replica for this version. A database advisory lock
prevents two workers from executing concurrently. Executable packages are trusted
software running with the worker's permissions; they do not have an OS sandbox.
The containers run Linux, so Windows/macOS binaries cannot execute in this stack.
Add any package-specific browser and system dependencies to `infra/containers/automations-worker.Dockerfile`.
Interactive login/CAPTCHA requires a graphical server session; remote desktop is
not provided by the portal.

Automations can be removed from their table rows with **Remover**. Removal deletes the
installed package while retaining execution history and results. Queued or active
executions must finish or be cancelled first. Completed, failed and cancelled runs
can download a ZIP of available results, including partial output; secrets and
packaged runtime resources are excluded.

## Execution metrics and review

Metrics appear only in **Execuções**. They count installed packages, executions in
progress and finished executions across the account's full history. `completed`
and `failed` both count toward **Execuções concluídas**; `cancelled` does not.
A failed execution remains visibly marked **Requer revisão**. Finishing a job and
needing review are distinct concepts.

The portal polls for changes while preserving page scroll, selected execution and
expanded logs. Installed automations and catalog entries use tables. Results are
presented as a single ZIP download when files are available, rather than a long
list of individual links.

## Account and credential boundaries

Membership owns accounts, sessions and 2FA. Boards validates workspace membership;
automation packages, installations, uploads and executions are scoped to authorized
workspaces. Session revocation is revalidated on each authenticated request through
membership. The existing login and account routes remain available through the gateway.

GitHub repository and encrypted PAT belong to the platform and are managed by
platform administrators. The API-only `automation-keys` volume preserves the keys
and the platform-specific Data Protection purpose; workers never receive plaintext
PATs. Never delete key volumes during routine updates. See the
[architecture decision](../../decisions/001-platform-boundaries.md) for current boundaries.
