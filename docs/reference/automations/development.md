# Automation development in domus

The API lives in `src/services/automations`, its shared library in `Core/`, and the worker in `src/workers/automations`. Dockerfiles live in `infra/containers`; the only Compose file is under `infra/`. Package tooling lives in `infra/scripts/automations`, schema in `docs/schemas`, and tests in `tests/Automations.Tests` and `tests/automations`.

Run from the repository root:

```sh
npm --prefix tests run test:automations
npm --prefix tests run test:backend
(cd src && dotnet build Domus.slnx)
node infra/scripts/automations/validate-packages.mjs ../automations/src/*/automation.json
```

Manifests belong beside the automation's own source. See [manifest authoring](manifest-authoring.md). The package contract and legacy automation namespaces remain unchanged by the folder move. Synthetic fixtures live in `tests/fixtures` and are not production manifests.

Use `./infra/scripts/compose.sh up --build -d --wait` to run the whole platform. API and worker share `/data`, while only the API receives `/keys`. Membership authenticates the user; boards authorizes the workspace; GitHub settings are platform-wide. Browser integration checks run with `npm --prefix tests test` in isolated Compose resources.
