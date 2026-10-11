# domus

One portal for a law firm's boards, daily tasks and automations. The four APIs, MCP server and two workers run independently on .NET 10.

## Run

From the repository root:

```sh
./infra/scripts/compose.sh up --build -d --wait --wait-timeout 240
```

Open **http://localhost:8088**. Register, choose a profile photo and enroll an authenticator for two-factor authentication. One session covers all modules.

There is **one Compose file**, `infra/compose.yaml`. It starts ten services:

| Service | Responsibility |
| --- | --- |
| `portal` | React portal and Nginx gateway |
| `membership` | Registration, authentication, sessions, 2FA, account profiles and durable identity delivery |
| `boards` | Boards, projects, tasks, workspaces, permissions, invitations and public identity projections |
| `automations` | Catalog, platform GitHub configuration, installation requests, inputs and results |
| `boards-worker` | External synchronization for boards |
| `automations-worker` | Queued package installation and execution |
| `ai` | Model connections, personal OAuth, settings and assistant orchestration |
| `mcp-server` | Independent MCP server exposing authenticated reads and reviewed actions |
| `postgres` | One PostgreSQL 18 instance with `membership`, `boards`, `automations` and `ai` databases |
| `rabbitmq` | Persistent installation and execution queues |

Only the portal exposes a host port. Each database has its own application role. Membership uses the boards role to provision workspaces, maintain public identity projections and accept invitations; passwords, sessions and authenticator secrets belong to membership. Automation files are shared between its API and worker; PAT encryption keys are mounted only into the API.

## Repository

```text
src/
  Domus.slnx                    .NET solution and shared build configuration
  portal/                       Shared React portal
  services/
    membership/                 Identity API
    boards/                     Boards API
    automations/                Automation API and shared Core library
    ai/                         AI assistance API
    mcp/                        Independent MCP server
  workers/
    boards/                     External synchronization worker
    automations/                Installation and execution worker
docs/                           Architecture, operations and authoring guides
  schemas/automation.schema.json
.github/workflows/              Image publication workflows
infra/
  compose.yaml                  Single deployment definition
  containers/                   Dockerfiles, proxy and PostgreSQL initialization
  scripts/automations/           Package authoring and verification tools
tests/                          Integration tests, unit tests and synthetic fixtures
README.md
LICENSE.md
```

The original project namespaces remain compatible. The root contains only README.md and LICENSE.md as visible files. Hidden `.gitignore` and `.dockerignore` retain standard Git/build behavior. The project is licensed under MIT; see LICENSE.md.

## Configuration

Copy `infra/.env.example` to `infra/.env` and choose private database and RabbitMQ passwords before hosting. `DOMUS_PUBLIC_URL` must match the browser origin, including `DOMUS_PORT`. Configure HTTPS at your public gateway. `DOMUS_ALLOWED_HOSTS` lists accepted API hosts separated by `;`; retain `localhost` for internal health checks.

`PLATFORM_ADMIN_EMAILS` identifies existing accounts allowed to manage platform appearance and GitHub access. After registering an account, set its email and recreate `membership` and `boards` to load the administrators. Workspace ownership alone does not confer platform administration.

**Settings → Automations** configures one repository and one encrypted PAT for the entire platform. The catalog uses this configuration in every workspace. Tokens are never returned to the browser. Workspace owners manage installation/removal; members execute and consult their workspace's runs. Logs, uploads, packages and results remain isolated by workspace. Completed runs count as completed even when they require review. Results are downloaded as ZIP archives.

The sidebar contains Overview, Management (including Boards), Automations and Settings. Groups begin collapsed and act as an accordion. Overview combines projects and execution metrics. The default brand is lowercase **domus**, with client cyan blues and a scales/circuit symbol.

## Verification

Use Node.js 22.13+, .NET SDK 10 and Chrome for browser integration tests:

```sh
npm --prefix tests ci
npm --prefix src/portal ci
npm --prefix tests run check
npm --prefix tests run build
npm --prefix tests run test:automations
npm --prefix tests run test:backend
npm --prefix tests run test:boards
npm --prefix tests test
```

`npm --prefix tests test` builds the same Compose in a separate project with temporary credentials, port and volumes. It checks registration, 2FA, shared sessions, permissions, platform settings, execution, ZIP downloads and the portal. It deletes only its own resources. `npm --prefix tests run test:boards` runs the inherited boards regression suite against its own temporary PostgreSQL container and also requires Python 3. Select a Docker context with `DOCKER_CONTEXT=your-context npm --prefix tests test`.

## Operations and documentation

```sh
./infra/scripts/compose.sh ps
./infra/scripts/compose.sh logs -f membership boards automations automations-worker
./infra/scripts/compose.sh down
```

`down` preserves the five persistent volumes; `down -v` deletes data and encryption keys. PostgreSQL database initialization runs only on an empty volume. Back up databases, shared automation files, keys and queue state together. Existing installations with two PostgreSQL servers need a logical database migration before starting this version; never mount PostgreSQL 17 data directly into PostgreSQL 18. See [database migration](docs/database-migration.md).

The [architecture decision](docs/decisions/001-platform-boundaries.md) documents service boundaries. The [automation authoring guide](docs/reference/automations/manifest-authoring.md) documents manifests stored alongside each automation and the [schema](docs/schemas/automation.schema.json).

`.github/workflows/publish-images.yml` publishes affected application images on pushes to `main` (stable) and `develop` (preview). The eight image suffixes are `portal`, `ai`, `mcp`, `membership`, `boards`, `automations`, `boards-worker` and `automations-worker`, under `ghcr.io/<owner>/<repository>-<component>`. Images support AMD64/ARM64 and receive a version tag, their channel alias (`stable` or `preview`), and a commit tag; stable builds also retain `latest`. The automatic GitHub token supplies package publication permissions; the workflow does not deploy the platform.

The workflow compares the entire pushed commit range, including deletions and moves:

| Changed code or configuration | Images published |
| --- | --- |
| Portal or its Nginx configuration | portal |
| Membership | membership |
| Boards | boards, membership, boards-worker (shared project references) |
| Boards worker | boards-worker |
| Automation API | automations |
| Automation Core or package schema | automations, automations-worker |
| Automation worker | automations-worker |
| A component's Dockerfile | That component |
| Shared .NET SDK/build configuration | All five .NET images |
| Docker ignore rules or publication workflow/tagging scripts | All six images |
| Documentation, tests or Compose-only changes | None |

The first push to a new branch has no previous revision and publishes all images. A manual workflow dispatch also rebuilds all images; push-based publication runs on `main` and `develop`. Unchanged images keep their existing tags. Run detector regression checks with `npm --prefix tests run test:ci`.

## Automatic image tags

Channel tags are assigned automatically:

| Channel | Automatic branch | Version tag | Moving alias | Commit tag |
| --- | --- | --- | --- | --- |
| Stable | `main` | `v0.1.0` | `stable` and `latest` | `sha-<full-commit-sha>` |
| Preview | `develop` | `prev-v0.1.0` | `preview` | `prev-sha-<full-commit-sha>` |

`stable` points to the most recently published stable image of each component; `preview` points to its most recently published preview image. Preview builds do not update stable aliases. The workflow still publishes only affected components, so an unchanged image's channel alias remains on its previous version.

Manual workflow runs offer a **channel** choice, defaulting to preview. Preview publication is allowed from any selected branch, including main. Stable publication requires main and is rejected from other branches before publishing. `latest` remains a compatibility alias for stable.

The release series is configured in `.github/image-version.json`:

```json
{
  "major": 0,
  "minor": 1,
  "patch": 0,
  "first_run": 1
}
```

Patch is calculated as `patch + GITHUB_RUN_NUMBER - first_run`. The default series therefore produces `v0.1.0`, `v0.1.1`, `v0.1.2`, and so on. All affected images in one workflow share a version. Retries reuse that version, including reruns of only failed jobs. Stable versions use exactly `vMAJOR.MINOR.PATCH`; preview versions add the requested `prev-` prefix, producing `prev-vMAJOR.MINOR.PATCH`.

For a planned minor or major release, update major/minor and set `first_run` to the next publication workflow's run number, with patch set to 0. For example, major 1, minor 0 and first_run 42 produces `v1.0.0` on run 42 and `v1.0.1` on run 43. Changing the version configuration republishes all six images.

Workflow numbers are shared across both channels and include documentation-only runs and failed runs, so patches can have gaps. Unchanged components retain their previous versions. No repository tag or GitHub release is required. Manual preview publication updates only preview tags, even when run from main. The OCI `org.opencontainers.image.version` label records the same semantic version.

Use a version tag or image digest to pin deployments. Registry tags remain mutable, and retries may replace a version tag; an image digest pins exact bytes. Examples: `ghcr.io/<owner>/<repository>-portal:stable`, `ghcr.io/<owner>/<repository>-portal:preview`, and `ghcr.io/<owner>/<repository>-portal:prev-v0.1.0`.

The workflow uses GitHub's [run variables](https://docs.github.com/en/actions/reference/workflows-and-actions/variables) and Docker's [metadata action](https://github.com/docker/metadata-action) to generate tags and image labels.

## AI assistance

Card suggestions and the workspace assistant use a personal ChatGPT subscription connected through OAuth in workspace AI settings. See [AI configuration and deployment](docs/ai.md) and [the separate MCP service architecture](docs/ai-architecture.md). There are no API-key fields or monthly limits. Workspace AI is disabled until enabled by its owner. For the local open-source flow, use `http://127.0.0.1:8088` and `AI_OPENAI_OAUTH_MODE=opensource`.

See [service boundaries and identity delivery](docs/domain-boundaries.md) for domain ownership, service names and upgrade steps.
