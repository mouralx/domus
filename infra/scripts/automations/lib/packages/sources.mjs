// Both acquisition routes must feed the same package importer and manifest validator.
export function describePackageSource(source) {
  if (!source || typeof source !== 'object') throw new Error('Package source is required');
  if (source.type === 'zip') {
    if (typeof source.filename !== 'string' || !source.filename.toLowerCase().endsWith('.zip')) throw new Error('A local ZIP filename is required');
    return { type: 'zip', filename: source.filename };
  }
  if (source.type === 'github') {
    if (typeof source.owner !== 'string' || typeof source.repository !== 'string' || !/^[A-Za-z0-9][A-Za-z0-9-]*$/.test(source.owner || '') || !/^[A-Za-z0-9][A-Za-z0-9_.-]*$/.test(source.repository || '')) throw new Error('Invalid GitHub repository');
    return { type: 'github', owner: source.owner, repository: source.repository, releasesUrl: `https://api.github.com/repos/${encodeURIComponent(source.owner)}/${encodeURIComponent(source.repository)}/releases` };
  }
  throw new Error('Unsupported package source');
}
