# Publishing domus images

The workflow `.github/workflows/publish-images.yml` selects images affected by the full pushed commit range on `main`. Image suffixes are portal, membership, boards, automations, boards-worker and automation-worker. Dockerfiles live under `infra/containers` and use the repository root as build context. Images publish to GHCR for AMD64 and ARM64 with `latest` and commit SHA tags. The automatic GitHub token supplies package write permission; deployment remains separate.

Portal-only changes publish only the portal. Boards code also rebuilds membership and the boards worker because they reference the boards project. Automation Core and schema changes rebuild the automation API and worker. Component Dockerfiles affect their own images; shared .NET configuration affects the five .NET images. Changes to the publication workflow/detector or Docker ignore rules rebuild all six. Documentation, tests and Compose-only changes do not publish images. Deleted and renamed paths are included. First pushes and manual workflow dispatch publish all six images.

The [root README](../../../README.md) has the full change-to-image mapping. Detector regression tests run with `npm --prefix tests run test:ci`.

Automation release packages use their own repository workflows. Keep each `automation.json` beside its automation source, include the generated version and compatible platform in the package, and attach the package to a GitHub release. See [manifest authoring](manifest-authoring.md). A platform administrator configures the repository and encrypted PAT through Settings → Automations.
