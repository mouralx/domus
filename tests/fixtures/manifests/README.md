# Synthetic manifest fixtures

These files are test inputs for schema validation, package tooling and Compose QA.
They are not maintained release metadata. Real automation manifests live beside
application source in the automation repository: `src/<automation-id>/automation.json`.
See [Manifest authoring](../../../docs/manifest-authoring.md) for the production process.
Tests must run independently of an automation repository checkout.
