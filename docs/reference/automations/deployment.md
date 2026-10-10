# Automation deployment

Use the single `infra/compose.yaml`:

```sh
./infra/scripts/compose.sh up --build -d --wait --wait-timeout 240
```

The API builds with `infra/containers/automations-api.Dockerfile`; the worker builds with `infra/containers/automations-worker.Dockerfile`. Both target .NET 10 and use the root build context. The worker installs and executes queued packages through RabbitMQ. API/worker share the automation-data volume; encryption keys belong only to the API.

One PostgreSQL instance hosts three separate databases. Automations uses `automations`; membership owns authentication and boards owns workspace authorization. The portal gateway exposes the UI and forwards API requests. All container health checks must pass before the deployment is ready.

See the [root README](../../../README.md) for configuration, image publication and operations, and the [migration guide](../../database-migration.md) before updating an existing two-server installation.
