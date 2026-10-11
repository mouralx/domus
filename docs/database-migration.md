# Migrating an existing domus installation

A fresh installation creates the three databases automatically. An existing installation with `domus_work` on PostgreSQL 17 and `domus_automations` on PostgreSQL 18 requires a logical migration. Changing Compose alone does not transfer data.

The helper `infra/scripts/migrate-databases.py` targets the previous local domus deployment. Run it only after validating the new images. It stops writers, saves private logical backups under `.local/database-backups/`, restores boards and automations into the new PostgreSQL volume, extracts identity tables into membership and removes authentication material from boards. Original database volumes remain available. Shared files, RabbitMQ and both encryption-key volumes keep their names and are not deleted.

```sh
python3 infra/scripts/migrate-databases.py --context your-docker-context
```

The helper refuses to run when a membership database already exists in the source server. It verifies restored row counts before restarting the application. If restore fails, application writers remain stopped: retain the backups, inspect the failure and restore into a clean target database before restarting. Do not run `./infra/scripts/compose.sh down -v` during migration.

Backups contain account hashes, encrypted secrets and application data. Their directory is private; preserve them securely. For production, also take consistent backups of the automation-data, work-keys, automation-keys and RabbitMQ volumes, and test recovery. Changing encryption keys would invalidate existing PATs and authenticator/Jira secrets.

The automation API migrates an unambiguous legacy repository/PAT pair into `platform_github_settings`, using the existing keys and a platform-specific protection purpose. If previous workspaces configured different repositories or tokens, legacy rows remain intact and a platform administrator must choose the desired pair. Set `PLATFORM_ADMIN_EMAILS` to existing account email addresses and recreate membership/boards to manage platform settings.

The Compose database initialization script runs only on an empty `platform-db` volume. Password changes in `infra/.env` do not automatically change existing PostgreSQL roles. Adjust roles explicitly when rotating credentials.
