import path from 'node:path';
import { assertManifest, resolvePackagePath } from './manifest.mjs';

// Pure planning only: this module does not install packages or spawn processes.
export function createRunPlan(manifest, values, { packageDirectory, runDirectory, platform = process.platform, arch = process.arch }) {
  assertManifest(manifest);
  if (manifest.platform.os !== platform || manifest.platform.arch !== arch) throw new Error('Package is not compatible with this local agent');
  if (!packageDirectory || !runDirectory) throw new Error('Separate package and fresh run directories are required');
  const installed = path.resolve(packageDirectory), working = path.resolve(runDirectory);
  if (installed === working || installed.startsWith(working + path.sep) || working.startsWith(installed + path.sep)) throw new Error('Separate package and fresh run directories are required');
  if (!values || typeof values !== 'object' || Array.isArray(values)) throw new Error('Input values must be an object');
  const knownInputs = new Set(manifest.inputs.map(input => input.id));
  for (const key of Object.keys(values)) if (!knownInputs.has(key)) throw new Error(`Unknown input: ${key}`);
  const args = [...manifest.execution.arguments];
  const envFileValues = Object.create(null);
  const env = Object.create(null);
  env[manifest.execution.homeEnvironmentVariable] = path.resolve(runDirectory);
  for (const [key, relative] of Object.entries(manifest.execution.packagePaths)) env[key] = resolvePackagePath(packageDirectory, relative);
  if (manifest.execution.stateDirectoryArgument) args.push(manifest.execution.stateDirectoryArgument, resolvePackagePath(runDirectory, '.state'));
  for (const input of manifest.inputs) {
    const supplied = Object.hasOwn(values, input.id) ? values[input.id] : input.default;
    if (supplied === undefined || supplied === '') {
      if (input.required) throw new Error(`Missing required input: ${input.id}`);
      continue;
    }
    if (input.type === 'integer') {
      if (!Number.isInteger(supplied) || (input.minimum !== undefined && supplied < input.minimum) || (input.maximum !== undefined && supplied > input.maximum)) throw new Error(`Invalid integer input: ${input.id}`);
    } else if (typeof supplied !== 'string' || supplied.includes('\u0000')) throw new Error(`Invalid text input: ${input.id}`);
    let value = String(supplied);
    if (input.type === 'file') {
      value = resolvePackagePath(runDirectory, value);
      if (input.extensions?.length && !input.extensions.includes(path.extname(value).toLowerCase())) throw new Error(`Unsupported file extension: ${input.id}`);
    }
    if (input.binding.argument) args.push(input.binding.argument, value);
    else envFileValues[input.binding.envFileKey] = value;
  }
  return {
    executable: resolvePackagePath(packageDirectory, manifest.execution.executable),
    arguments: args,
    workingDirectory: path.resolve(runDirectory),
    environment: env,
    environmentFileValues: envFileValues,
    workspaceResources: manifest.execution.workspaceResources.map(relative => ({ source: resolvePackagePath(packageDirectory, relative), destination: resolvePackagePath(runDirectory, relative) })),
    shell: false,
    authentication: { ...manifest.authentication },
    effects: { ...manifest.effects }
  };
}
