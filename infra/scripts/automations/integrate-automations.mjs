import { writeFile } from 'node:fs/promises';
import { planAutomationIntegration } from './lib/packages/integration.mjs';
const [repository, mode] = process.argv.slice(2);
if (!repository || (mode && mode !== '--apply')) throw new Error('Usage: node scripts/integrate-automations.mjs <automation-repository> [--apply]');
const changes = await planAutomationIntegration(repository);
for (const change of changes) {
  if (mode === '--apply') await writeFile(change.path, change.content);
  console.log(`${mode === '--apply' ? 'Updated' : 'Would update'} ${change.path}`);
}
if (!changes.length) console.log('Package metadata integration is already up to date.');
