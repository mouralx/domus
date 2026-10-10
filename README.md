# domus

One portal for a law firm's boards, daily tasks and automations. The three APIs and two workers run independently on .NET 10.

## Run

From the repository root:

```sh
./infra/scripts/compose.sh up --build -d --wait --wait-timeout 240
```

Open **http://localhost:8088**. Register, choose a profile photo and enroll an authenticator for two-factor authentication. One session covers all modules.

There is **one Compose file**, `infra/compose.yaml`. It starts eight services:

| Service | Responsibility |
| --- | --- |
| `portal` | React portal and Nginx gateway |
| `membership` | Registration, authentication, sessions, 2FA, account profiles and invitation acceptance |
| `work-api` | Boards, projects, tasks, workspaces, permissions and public identity projections |
| `automation-api` | Catalog, platform GitHub configuration, installation requests, inputs and results |
| `jira-worker` | External synchronization for boards |
| `automation-worker` | Queued package installation and execution |
| `postgres` | One PostgreSQL 18 instance with `membership`, `boards` and `automations` databases |
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

The original project namespaces remain compatible. The root contains only README.md and LICENSE.md as visible files. Hidden `.gitignore` and `.dockerignore` retain standard Git/build behavior. No software licence has been selected; LICENSE.md records this status.

## Configuration

Copy `infra/.env.example` to `infra/.env` and choose private database and RabbitMQ passwords before hosting. `DOMUS_PUBLIC_URL` must match the browser origin, including `DOMUS_PORT`. Configure HTTPS at your public gateway. `DOMUS_ALLOWED_HOSTS` lists accepted API hosts separated by `;`; retain `localhost` for internal health checks.

`PLATFORM_ADMIN_EMAILS` identifies existing accounts allowed to manage platform appearance and GitHub access. After registering an account, set its email and recreate `membership` and `work-api` to load the administrators. Workspace ownership alone does not confer platform administration.

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
./infra/scripts/compose.sh logs -f membership work-api automation-api automation-worker
./infra/scripts/compose.sh down
```

`down` preserves the five persistent volumes; `down -v` deletes data and encryption keys. PostgreSQL database initialization runs only on an empty volume. Back up databases, shared automation files, keys and queue state together. Existing installations with two PostgreSQL servers need a logical database migration before starting this version; never mount PostgreSQL 17 data directly into PostgreSQL 18. See [database migration](docs/database-migration.md).

The [architecture decision](docs/decisions/001-platform-boundaries.md) documents service boundaries. The [automation authoring guide](docs/reference/automations/manifest-authoring.md) documents manifests stored alongside each automation and the [schema](docs/schemas/automation.schema.json).

`.github/workflows/publish-images.yml` publishes affected application images on every push to `main`. The six image suffixes are `portal`, `membership`, `boards`, `automations`, `boards-worker` and `automation-worker`, under `ghcr.io/<owner>/<repository>-<component>`. Images support AMD64/ARM64 and receive an automatic calendar version, `latest` on main, and a full commit SHA tag. The automatic GitHub token supplies package publication permissions; the workflow does not deploy the platform.

The workflow compares the entire pushed commit range, including deletions and moves:

| Changed code or configuration | Images published |
| --- | --- |
| Portal or its Nginx configuration | portal |
| Membership | membership |
| Boards | boards, membership, boards-worker (shared project references) |
| Boards worker | boards-worker |
| Automation API | automations |
| Automation Core or package schema | automations, automation-worker |
| Automation worker | automation-worker |
| A component's Dockerfile | That component |
| Shared .NET SDK/build configuration | All five .NET images |
| Docker ignore rules or publication workflow/tagging scripts | All six images |
| Documentation, tests or Compose-only changes | None |

The first push to a new branch has no previous revision and publishes all images. A manual workflow dispatch also rebuilds all images; push-based publication remains limited to `main`. Unchanged images keep their existing tags. Run detector regression checks with `npm --prefix tests run test:ci`.

## Automatic image tags

Every affected image receives these tags automatically:

| Tag | Purpose |
| --- | --- |
| `v2026.10.10.42.1` | Pin a particular build: `vYYYY.MM.DD.RUN.ATTEMPT` |
| `sha-<full-commit-sha>` | Locate the most recent publication of a commit |
| `latest` | Most recently published main-branch build of that component |

The date is UTC and selected once per workflow run. GitHub supplies the workflow run number and attempt number. Each publish job reads its current attempt, so rerunning only failed jobs produces a new version suffix while keeping the original shared date. A full rerun may select a new UTC date. These are calendar versions, not semantic versions; no manual bump, repository tag or release is required. Version tags are unique per run/attempt under this workflow; the registry does not enforce immutability. Use a version tag or image digest to pin deployments, rather than the moving `latest` or commit aliases.

All affected components in the same attempt share a version. Components that did not change retain their previous published versions; workflow numbers can therefore have gaps for an individual image. Documentation-only pushes do not publish or move image tags. Manual publication outside main produces the version and commit tags without updating `latest`. The OCI `org.opencontainers.image.version` label records the generated calendar version.

For example: `ghcr.io/<owner>/<repository>-portal:v2026.10.10.42.1`.

The workflow uses GitHub's [run and attempt variables](https://docs.github.com/en/actions/reference/workflows-and-actions/variables) and Docker's [metadata action](https://github.com/docker/metadata-action) to generate tags and image labels.
