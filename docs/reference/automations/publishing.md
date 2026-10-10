# Publishing domus images

The workflow `.github/workflows/publish-images.yml` selects images affected by the full pushed commit range on `main` for stable and `develop` for preview. Image suffixes are portal, membership, boards, automations, boards-worker and automation-worker. Dockerfiles live under `infra/containers` and use the repository root as build context. Images publish to GHCR for AMD64 and ARM64 with stable `vMAJOR.MINOR.PATCH`/`stable` tags or preview `prev-vMAJOR.MINOR.PATCH`/`preview` tags. Stable builds retain `latest` for compatibility. Commit tags use `sha-<full-commit-sha>` for stable and `prev-sha-<full-commit-sha>` for preview. The automatic GitHub token supplies package write permission; deployment remains separate.

Portal-only changes publish only the portal. Boards code also rebuilds membership and the boards worker because they reference the boards project. Automation Core and schema changes rebuild the automation API and worker. Component Dockerfiles affect their own images; shared .NET configuration affects the five .NET images. Changes to the publication workflow/tagging scripts or Docker ignore rules rebuild all six. Documentation, tests and Compose-only changes do not publish images. Deleted and renamed paths are included. First pushes and manual workflow dispatch publish all six images.

The [root README](../../../README.md) has the full change-to-image mapping. Detector regression tests run with `npm --prefix tests run test:ci`.

Automation release packages use their own repository workflows. Keep each `automation.json` beside its automation source, include the generated version and compatible platform in the package, and attach the package to a GitHub release. See [manifest authoring](manifest-authoring.md). A platform administrator configures the repository and encrypted PAT through Settings → Automations.

## Automatic versioning

`.github/image-version.json` defines major, minor, initial patch and first_run. Patch is `initial patch + GITHUB_RUN_NUMBER - first_run`; the default configuration produces `v0.1.0`, `v0.1.1`, and so on. All affected components share the workflow's version. Retries reuse the same version. Preview versions add `prev-`, for example `prev-v0.1.0`; stable versions remain `v0.1.0`.

For a planned major/minor release, update the configuration and set first_run to the next publication run number. Version configuration changes republish all six images. Both channels, documentation-only and failed workflows consume run numbers, so patch numbers can have gaps. Unchanged images retain their previous versions. No repository tag or manual patch bump is needed.

Use an image digest to pin exact bytes. Version tags can be replaced on retries. Channel aliases and commit tags are moving aliases. `stable` tracks the latest stable publication; `preview` tracks the latest preview publication for each component. Manual runs default to preview and allow a channel choice. Stable publication is restricted to main; preview publication never updates `stable` or `latest`, including manual previews from main. Each image records its version in `org.opencontainers.image.version`. Change detection and versioning tests run with `npm --prefix tests run test:ci`. See the [root README](../../../README.md) for configuration and examples.
