# Publishing domus images

The workflow `.github/workflows/publish-images.yml` selects images affected by the full pushed commit range on `main`. Image suffixes are portal, membership, boards, automations, boards-worker and automation-worker. Dockerfiles live under `infra/containers` and use the repository root as build context. Images publish to GHCR for AMD64 and ARM64 with automatic `vYYYY.MM.DD.RUN.ATTEMPT`, full commit SHA tags, and `latest` for main. The automatic GitHub token supplies package write permission; deployment remains separate.

Portal-only changes publish only the portal. Boards code also rebuilds membership and the boards worker because they reference the boards project. Automation Core and schema changes rebuild the automation API and worker. Component Dockerfiles affect their own images; shared .NET configuration affects the five .NET images. Changes to the publication workflow/tagging scripts or Docker ignore rules rebuild all six. Documentation, tests and Compose-only changes do not publish images. Deleted and renamed paths are included. First pushes and manual workflow dispatch publish all six images.

The [root README](../../../README.md) has the full change-to-image mapping. Detector regression tests run with `npm --prefix tests run test:ci`.

Automation release packages use their own repository workflows. Keep each `automation.json` beside its automation source, include the generated version and compatible platform in the package, and attach the package to a GitHub release. See [manifest authoring](manifest-authoring.md). A platform administrator configures the repository and encrypted PAT through Settings → Automations.

## Automatic versioning

Calendar tags use a shared UTC build date, the GitHub workflow run number and the current attempt number, for example `v2026.10.10.42.1`. Only affected images are published; each component may have gaps in its run numbers. Failed-job reruns read the current attempt in the publishing job and retain the selection job's date, avoiding reuse of the original attempt's version. Full reruns can select a new UTC date. No repository tag or manual version bump is needed.

Use the version tag or image digest for deployment pinning. `latest` and `sha-<full-commit-sha>` are moving aliases; the registry does not enforce immutable version tags. Manual publication outside main does not update `latest`. Each image stores its generated version in `org.opencontainers.image.version`. Tests for change detection and version generation run with `npm --prefix tests run test:ci`.
