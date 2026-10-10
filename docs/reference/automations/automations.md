> Referência do módulo de origem. Para executar a plataforma integrada Domus, consulte o [README principal](../../../README.md). O único Compose está em `infra/compose.yaml`.

# Automation packages and catalog

Run authoring commands below from the repository root.

In **Definições**, save the GitHub repository as `owner/repository` or its GitHub
URL, together with an optional PAT granting **Contents: read**. Both settings are platform-wide and managed by platform administrators. Changing the repository without entering a new PAT preserves
the encrypted token; removing the token preserves the repository configuration.

**Catálogo** loads automatically and provides a searchable table with one entry per
automation, using its latest compatible release. Entries show **Instalar**,
**Atualizar**, or **Instalada** by comparing numeric versions against installed
packages. Installing/updating creates a PostgreSQL installation job plus an outbox entry;
RabbitMQ delivers its ID to the worker. The catalog polls persisted state and shows
queued, installing, success or error status. Duplicate submissions for the same
automation are blocked while pending. The worker validates and activates packages
on the shared volume; the API only streams authenticated GitHub bytes through an
internal endpoint, so PATs and API encryption keys remain outside the worker.
Updating replaces the installed version without deleting run history or
results. Queued/active executions block replacement until finished or cancelled.
The **Importar ZIP** screen has been removed; the old import API remains for
compatibility and synthetic test setup.

Release ZIP names follow `AppName-linux-arm64-v1.2.3.zip` or
`AppName-linux-amd64-v1.2.3.zip`, with tags such as `automation-id-v1.2.3`.
The archive's `automation.json` must match the advertised ID, version, platform and
architecture. Optional `src/<automation-id>/automation.json` metadata at the release
tag supplies friendly names/descriptions before installation. Windows/macOS assets
are filtered out of this Linux deployment; draft/prerelease releases are excluded.

PATs are encrypted in PostgreSQL using ASP.NET Core Data Protection, with a platform-specific
protection purpose. Encryption keys persist in the API-only
`automation-keys` volume. Back up that key volume alongside PostgreSQL. Tokens are
never returned to the browser or forwarded to asset redirect hosts. An optional
server-wide `GITHUB_TOKEN` remains a fallback when no platform PAT is saved.

## Package contract v1

A package contains one `automation.json` beside its executable and runtime assets.
A ZIP may have one outer package directory. The schema is
[automation.schema.json](../../schemas/automation.schema.json). Production
manifest sources live beside their automation code, at
`src/<automation-id>/automation.json` in the automation repository. The portal
contains only synthetic test fixtures, not a second source of production metadata.
See [Manifest authoring](manifest-authoring.md) for fields, validation and builds.

The manifest describes identity/version/platform, process entry point, guided
inputs and bindings, authentication, expected outputs and meaningful effects.
Executables launch directly with argument arrays (`shell: false`). Runtime assets
stay in the installed package; declared `workspaceResources` are copied into each
fresh workspace and cannot overwrite staged inputs. Secrets cannot have defaults
or command-line bindings. Publish no credentials or real user data in packages.

Archive installation validates contained paths, duplicate names, symlinks, size limits,
manifest shape, entry-point existence and resource existence. Limits are 1.5 GiB
compressed, 2.5 GiB extracted, 800 MiB per file and 60,000 entries; manifests are
limited to 256 KiB and input uploads to 80 MiB. Installation does not execute code.
Installed packages run with the worker container user's permissions. Install only
packages from authors you trust; there is no OS sandbox for automation code.

GitHub catalog installations pin the selected asset and record its SHA-256. When
GitHub provides an asset digest, it is verified before installation. Publisher
text is descriptive, not an authenticated signature. Older automation ZIPs without
`automation.json` must be rebuilt before they can be installed.

The integration tool reads and validates each automation-owned manifest without
creating or replacing it. It patches recognized Windows build scripts to emit metadata with the actual
release version and includes optional structured login-status events. It also excludes real
workbooks, downloads and backups from new automation distributions. To apply it:

```sh
node infra/scripts/automations/integrate-automations.mjs ../automations --apply
```

For another package author, emit the manifest into an already-built package before
creating the ZIP:

```sh
node infra/scripts/automations/prepare-package.mjs ../automations/src/pending-records/automation.json /path/to/package 1.0.7
```

The status protocol accepts plain stdout/stderr and an optional newline-delimited
`@@automation-event@@{"type":"interaction","state":"awaiting-user"}` message.
Use `state: "running"` after returning to headless. Other stdout remains in the local
log. Progress is indeterminate; no percentages are guessed from arbitrary text.


## Portal workflow

1. Register or sign in and complete 2FA. A platform administrator configures GitHub access in **Settings → Automations**.
2. Open **Catálogo**, search by name or description, and choose **Instalar** or
   **Atualizar**. Wait for the worker to finish; installation does not run the automation.
3. Open **Automações** and choose **Executar**. Supply the requested inputs.
4. Follow progress in **Execuções**, inspect any review status and download results.
5. Use **Remover** in the installed automations table when no execution or
   installation is pending. History and existing results are retained.

Repository configuration is managed in Settings; the catalog has no repository
configuration form or ZIP upload screen. Its refresh button and search input share
one row. Searches ignore letter case and accents.
