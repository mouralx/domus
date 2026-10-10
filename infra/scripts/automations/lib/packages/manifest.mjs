import { readFileSync } from 'node:fs';
import path from 'node:path';

export const manifestSchema = JSON.parse(readFileSync(new URL('../../../../../docs/schemas/automation.schema.json', import.meta.url), 'utf8'));
const isObject = value => value !== null && typeof value === 'object' && !Array.isArray(value);
const matchesType = (value, type) => type === 'object' ? isObject(value) : type === 'array' ? Array.isArray(value) : type === 'integer' ? Number.isInteger(value) : typeof value === type;

// The manifest schema uses only the JSON Schema keywords implemented here.
function check(schema, value, location, errors) {
  if (schema.oneOf) {
    const matches = schema.oneOf.filter(candidate => {
      const candidateErrors = [];
      check(candidate, value, location, candidateErrors);
      return candidateErrors.length === 0;
    });
    if (matches.length !== 1) errors.push(`${location}: must match exactly one supported shape`);
    return;
  }
  if (schema.type && ![].concat(schema.type).some(type => matchesType(value, type))) {
    errors.push(`${location}: expected ${[].concat(schema.type).join(' or ')}`);
    return;
  }
  if ('const' in schema && value !== schema.const) errors.push(`${location}: expected ${JSON.stringify(schema.const)}`);
  if (schema.enum && !schema.enum.includes(value)) errors.push(`${location}: unsupported value`);
  if (typeof value === 'string') {
    if (schema.maxLength && value.length > schema.maxLength) errors.push(`${location}: text is too long`);
    if (schema.minLength && value.trim().length < schema.minLength) errors.push(`${location}: must not be empty`);
    if (schema.pattern && !new RegExp(schema.pattern).test(value)) errors.push(`${location}: invalid format`);
  }
  if (Array.isArray(value) && schema.maxItems && value.length > schema.maxItems) errors.push(`${location}: too many items`);
  if (Array.isArray(value) && schema.items) value.forEach((item, index) => check(schema.items, item, `${location}[${index}]`, errors));
  if (isObject(value)) {
    for (const key of schema.required || []) if (!Object.hasOwn(value, key)) errors.push(`${location}.${key}: required`);
    for (const [key, item] of Object.entries(value)) {
      if (schema.propertyNames) check(schema.propertyNames, key, `${location} property name`, errors);
      if (schema.properties && Object.hasOwn(schema.properties, key)) check(schema.properties[key], item, `${location}.${key}`, errors);
      else if (schema.additionalProperties === false) errors.push(`${location}.${key}: unknown property`);
      else if (isObject(schema.additionalProperties)) check(schema.additionalProperties, item, `${location}.${key}`, errors);
    }
  }
}

export function validateManifest(manifest) {
  const errors = [];
  check(manifestSchema, manifest, 'manifest', errors);
  if (errors.length) return errors;
  if (Object.hasOwn(manifest.execution.packagePaths, manifest.execution.homeEnvironmentVariable)) errors.push('execution.packagePaths: cannot override the run home');
  const ids = new Set(), bindings = new Set();
  for (const input of manifest.inputs) {
    if (ids.has(input.id)) errors.push(`inputs.${input.id}: duplicate id`);
    ids.add(input.id);
    const binding = input.binding.argument || input.binding.envFileKey;
    if (bindings.has(binding)) errors.push(`inputs.${input.id}: duplicate binding`);
    bindings.add(binding);
    if (input.binding.argument && ['--dry-run', manifest.execution.stateDirectoryArgument].includes(binding)) errors.push(`inputs.${input.id}: reserved execution argument`);
    if (input.binding.envFileKey && [manifest.execution.homeEnvironmentVariable, ...Object.keys(manifest.execution.packagePaths)].includes(binding)) errors.push(`inputs.${input.id}: reserved environment variable`);
    if (input.type === 'file' && Object.hasOwn(input, 'default')) errors.push(`inputs.${input.id}: file inputs require explicit selection`);
    if (input.columns?.length && (!input.extensions?.includes('.xlsx') || input.extensions.some(extension => extension !== '.xlsx'))) errors.push(`inputs.${input.id}: column validation requires an Excel-only input`);
    if (input.type === 'secret' && Object.hasOwn(input, 'default')) errors.push(`inputs.${input.id}: secrets cannot have defaults`);
    if (input.type === 'secret' && !input.binding.envFileKey) errors.push(`inputs.${input.id}: secrets must use an environment file binding`);
    if (input.type !== 'file' && (input.columns || input.extensions)) errors.push(`inputs.${input.id}: file metadata requires a file input`);
    if (input.type !== 'integer' && ('minimum' in input || 'maximum' in input)) errors.push(`inputs.${input.id}: bounds require an integer input`);
    if (input.minimum !== undefined && input.maximum !== undefined && input.minimum > input.maximum) errors.push(`inputs.${input.id}: minimum exceeds maximum`);
    if (Object.hasOwn(input, 'default') && !matchesType(input.default, input.type === 'file' ? 'string' : input.type)) errors.push(`inputs.${input.id}: default has the wrong type`);
    if (typeof input.default === 'number' && ((input.minimum !== undefined && input.default < input.minimum) || (input.maximum !== undefined && input.default > input.maximum))) errors.push(`inputs.${input.id}: default outside bounds`);
  }
  return errors;
}

export function assertManifest(manifest) {
  const errors = validateManifest(manifest);
  if (errors.length) throw new Error(`Invalid automation manifest:\n${errors.join('\n')}`);
  return manifest;
}

export function resolvePackagePath(root, relative) {
  const errors = [];
  check(manifestSchema.properties.execution.properties.executable, relative, 'path', errors);
  if (errors.length) throw new Error('Expected a safe package-relative path');
  const directory = path.resolve(root);
  const result = path.resolve(directory, relative);
  if (result === directory || !result.startsWith(directory + path.sep)) throw new Error('Path escapes its directory');
  return result;
}
