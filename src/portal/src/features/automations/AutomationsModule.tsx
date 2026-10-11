import { readJsonResponse } from '../../infrastructure/jsonResponse';
import { Download } from 'lucide-react';
import { useI18n } from '../../shared/i18n';
import { useCallback, useEffect, useRef, useState } from 'react';

type Input = {
  id: string;
  label: string;
  type: string;
  required: boolean;
  description: string;
  extensions?: string[];
  minimum?: number;
  maximum?: number;
  default?: string | number;
  columns?: string[];
};
type Manifest = {
  id: string;
  name: string;
  description: string;
  version: string;
  inputs: Input[];
  effects?: { modifiesInput: boolean; publishesComments: boolean; createsPaidRequests: boolean };
};
type Package = { key: string; compatible: boolean; manifest: Manifest };
type Run = {
  id: string;
  name: string;
  status: string;
  message?: string;
  logs?: string[];
  files?: { size: number }[];
};
type Status = { packages: Package[]; runs: Run[]; metrics: { running: number; completed: number } };
type Entry = {
  assetId: string;
  automationId: string;
  name: string;
  description: string;
  version: string;
  installedVersion?: string;
  action: 'install' | 'update' | 'installed';
};
type Job = {
  id: string;
  status: string;
  error?: string;
  recipe: { assetId: string; automationId: string };
};

export function AutomationsModule({
  workspace,
  canManage,
  canManagePlatform,
  tab,
  initialRunId,
}: {
  workspace: string;
  canManage: boolean;
  canManagePlatform: boolean;
  tab: string;
  initialRunId?: string;
}) {
  const { t } = useI18n();
  const pendingJob = useRef(false);
  const installingIds = useRef(new Set<string>());
  const [submitting, setSubmitting] = useState<string[]>([]);
  const [status, setStatus] = useState<Status | null>(null);
  const [catalog, setCatalog] = useState<Entry[]>([]);
  const [jobs, setJobs] = useState<Job[]>([]);
  const [query, setQuery] = useState('');
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const [packageKey, setPackageKey] = useState('');
  const [selected, setSelected] = useState<Run | null>(null);
  const [repository, setRepository] = useState('');
  const [token, setToken] = useState('');
  const [saved, setSaved] = useState(false);
  const request = useCallback(
    async <T,>(path: string, init: RequestInit = {}): Promise<T> => {
      const headers = new Headers(init.headers);
      headers.set('X-Domus-Workspace', workspace);
      if (init.body && typeof init.body === 'string')
        headers.set('Content-Type', 'application/json');
      const response = await fetch('/automations-api/api' + path, {
        ...init,
        headers,
        credentials: 'include',
      });
      return readJsonResponse<T>(
        response,
        t('The automation service is unavailable. Please try again later.'),
      );
    },
    [workspace, t],
  );
  const refreshCatalog = useCallback(async () => {
    const value = await request<{ automations: Entry[] }>('/catalog');
    setCatalog(value.automations);
  }, [request]);
  useEffect(() => {
    let active = true;
    let polling = false;
    setStatus(null);
    setSelected(null);
    setPackageKey('');
    setCatalog([]);
    setError('');
    setToken('');
    const load = async (initial: boolean) => {
      if (polling) return;
      polling = true;
      try {
        const [next, installations] = await Promise.all([
          request<Status>('/status'),
          request<Job[]>('/installations'),
        ]);
        if (!active) return;
        setStatus(next);
        setJobs(installations);
        const nowPending = installations.some((job) =>
          ['queued', 'installing'].includes(job.status),
        );
        const finished = pendingJob.current && !nowPending;
        pendingJob.current = nowPending;
        if ((initial || finished) && tab === 'Catalog') {
          const value = await request<{ automations: Entry[] }>('/catalog');
          if (active) setCatalog(value.automations);
        }
        if (initial && tab === 'Executions' && initialRunId) {
          const run = await request<Run>('/runs/' + initialRunId);
          if (active) setSelected(run);
        }
        if (initial && tab === 'Automation settings' && canManagePlatform) {
          const config = await request<{ repository: string; saved: boolean }>('/settings/github');
          if (active) {
            setRepository(config.repository || '');
            setSaved(config.saved);
          }
        }
      } catch (reason) {
        if (active) setError(String(reason instanceof Error ? reason.message : reason));
      } finally {
        polling = false;
      }
    };
    void load(true);
    const timer = window.setInterval(() => void load(false), 5000);
    return () => {
      active = false;
      window.clearInterval(timer);
    };
  }, [request, tab, canManagePlatform, initialRunId]);
  const selectedId = selected?.id;
  useEffect(() => {
    if (!selectedId) return;
    let active = true;
    const timer = window.setInterval(() => {
      void request<Run>('/runs/' + selectedId)
        .then((value) => {
          if (active) setSelected(value);
        })
        .catch((reason) => {
          if (active) setError(String(reason.message));
        });
    }, 5000);
    return () => {
      active = false;
      window.clearInterval(timer);
    };
  }, [request, selectedId]);
  async function perform(action: () => Promise<void>) {
    setBusy(true);
    setError('');
    try {
      await action();
      setStatus(await request<Status>('/status'));
      setJobs(await request<Job[]>('/installations'));
    } catch (reason) {
      setError(String(reason instanceof Error ? reason.message : reason));
    } finally {
      setBusy(false);
    }
  }
  async function install(item: Entry) {
    if (installingIds.current.has(item.automationId)) return;
    installingIds.current.add(item.automationId);
    setSubmitting((current) => [...current, item.automationId]);
    setError('');
    try {
      const job = await request<{ id: string; status: string }>('/catalog/install', {
        method: 'POST',
        body: JSON.stringify({ assetId: item.assetId }),
      });
      pendingJob.current = true;
      setJobs((current) => [
        ...current.filter((value) => value.recipe.automationId !== item.automationId),
        { ...job, recipe: { assetId: item.assetId, automationId: item.automationId } },
      ]);
    } catch (reason) {
      setError(reason instanceof Error ? reason.message : String(reason));
    } finally {
      installingIds.current.delete(item.automationId);
      setSubmitting((current) => current.filter((id) => id !== item.automationId));
    }
  }
  const pkg = status?.packages.find((item) => item.key === packageKey);
  const text = (value: string) =>
    value
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .toLowerCase();
  const pendingCount = jobs.filter((job) => ['queued', 'installing'].includes(job.status)).length;
  const installationPending = (item: Entry) =>
    submitting.includes(item.automationId) ||
    jobs.some(
      (job) =>
        job.recipe.automationId === item.automationId &&
        ['queued', 'installing'].includes(job.status),
    );
  return (
    <section className="domus-automations">
      {error && (
        <p className="domus-error" role="alert">
          {error}
        </p>
      )}
      {!status && !error && <p>{t('Loading automations\u2026')}</p>}
      {tab === 'Automations' && status && (
        <>
          <div className="automation-table-scroll">
            <table>
              <thead>
                <tr>
                  <th>{t('Automation')}</th>
                  <th>{t('Version')}</th>
                  <th>{t('Actions')}</th>
                </tr>
              </thead>
              <tbody>
                {status.packages.map((item) => (
                  <tr key={item.key}>
                    <td>
                      <strong>{item.manifest.name}</strong>
                      <p>{item.manifest.description}</p>
                    </td>
                    <td>{item.manifest.version}</td>
                    <td>
                      <div className="automation-actions">
                        {canManage && (
                          <button
                            disabled={busy}
                            onClick={() => {
                              if (
                                confirm(
                                  t(
                                    'Remove this automation? History and results will be preserved.',
                                  ),
                                )
                              )
                                void perform(async () => {
                                  await request('/packages/remove', {
                                    method: 'POST',
                                    body: JSON.stringify({ packageKey: item.key }),
                                  });
                                });
                            }}
                          >
                            {t('Remove')}
                          </button>
                        )}
                        <button
                          className="automation-primary"
                          disabled={busy || !item.compatible}
                          onClick={() => setPackageKey(item.key)}
                        >
                          {t('Run')}
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!status.packages.length && (
            <p className="automation-empty">
              {t('Install your first automation from the catalog.')}
            </p>
          )}
          {pkg && (
            <form
              className="automation-panel"
              key={pkg.key}
              onSubmit={(event) => {
                event.preventDefault();
                const form = event.currentTarget;
                const values = new FormData(form);
                void perform(async () => {
                  const inputs: Record<string, string | number> = {};
                  for (const input of pkg.manifest.inputs) {
                    const value = values.get(input.id);
                    if (value instanceof File) {
                      if (value.size)
                        inputs[input.id] = (
                          await request<{ id: string }>(
                            '/uploads?name=' + encodeURIComponent(value.name),
                            { method: 'POST', body: value },
                          )
                        ).id;
                    } else if (value)
                      inputs[input.id] = input.type === 'integer' ? Number(value) : String(value);
                  }
                  await request('/runs', {
                    method: 'POST',
                    body: JSON.stringify({ packageKey: pkg.key, values: inputs }),
                  });
                  form.reset();
                  setPackageKey('');
                });
              }}
            >
              <h2>{pkg.manifest.name}</h2>
              {pkg.manifest.effects?.modifiesInput && (
                <p>{t('The file will be processed in a working copy.')}</p>
              )}
              {pkg.manifest.effects?.publishesComments && (
                <p>{t('This automation publishes comments on the platform.')}</p>
              )}
              {pkg.manifest.effects?.createsPaidRequests && (
                <p>{t('This automation may complete paid requests.')}</p>
              )}
              {pkg.manifest.inputs.map((input) => (
                <label key={input.id}>
                  {input.label}
                  <input
                    name={input.id}
                    defaultValue={input.type === 'file' ? undefined : input.default}
                    required={input.required}
                    type={
                      input.type === 'integer'
                        ? 'number'
                        : input.type === 'secret'
                          ? 'password'
                          : input.type === 'file'
                            ? 'file'
                            : 'text'
                    }
                    min={input.minimum}
                    max={input.maximum}
                    accept={input.extensions?.join(',')}
                    autoComplete="off"
                  />
                  <small>
                    {input.description}
                    {input.columns?.length
                      ? ' ' + t('Required columns: {0}.', input.columns.join(' · '))
                      : ''}
                  </small>
                </label>
              ))}
              <div className="automation-actions">
                <button className="automation-primary" disabled={busy}>
                  {t('Start execution')}
                </button>
                <button type="button" onClick={() => setPackageKey('')}>
                  {t('Close')}
                </button>
              </div>
            </form>
          )}
        </>
      )}
      {tab === 'Catalog' && (
        <>
          <div className="domus-catalog-toolbar">
            <label>
              {t('Search automations')}
              <input
                type="search"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder={t('Name or description')}
              />
            </label>
            <button disabled={busy} onClick={() => void perform(refreshCatalog)}>
              {t('Refresh catalog')}
            </button>
          </div>
          {pendingCount > 0 && (
            <p role="status">{t('{0} installation(s) queued or processing.', pendingCount)}</p>
          )}
          {jobs
            .filter((job) => job.status === 'failed')
            .map((job) => (
              <p className="domus-error" key={job.id}>
                {job.error}
              </p>
            ))}
          <div className="automation-table-scroll">
            <table>
              <thead>
                <tr>
                  <th>{t('Automation')}</th>
                  <th>{t('Available')}</th>
                  <th>{t('Installed')}</th>
                  <th>{t('Action')}</th>
                </tr>
              </thead>
              <tbody>
                {catalog
                  .filter((item) => text(item.name + ' ' + item.description).includes(text(query)))
                  .map((item) => (
                    <tr key={item.assetId}>
                      <td>
                        <strong>{item.name}</strong>
                        <p>{item.description}</p>
                      </td>
                      <td>{item.version}</td>
                      <td>{item.installedVersion || '—'}</td>
                      <td>
                        <button
                          className={item.action === 'installed' ? undefined : 'automation-primary'}
                          disabled={
                            installationPending(item) || !canManage || item.action === 'installed'
                          }
                          onClick={() => void install(item)}
                        >
                          {installationPending(item)
                            ? t('Installing…')
                            : {
                                install: t('Install'),
                                update: t('Update'),
                                installed: t('Installed'),
                              }[item.action]}
                        </button>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
          {!catalog.length && !error && (
            <p className="automation-empty">
              {t('No automations available. Check settings or refresh the catalog.')}
            </p>
          )}
        </>
      )}
      {tab === 'Executions' && status && (
        <>
          <div className="domus-metrics">
            <div>
              {t('Installed flows')}
              <strong>{status.packages.length}</strong>
            </div>
            <div>
              {t('Running executions')}
              <strong>{status.metrics.running}</strong>
            </div>
            <div>
              {t('Completed executions')}
              <strong>{status.metrics.completed}</strong>
            </div>
          </div>
          <div className="automation-table-scroll">
            <table>
              <thead>
                <tr>
                  <th>{t('Automation')}</th>
                  <th>{t('Status')}</th>
                  <th>{t('Action')}</th>
                </tr>
              </thead>
              <tbody>
                {status.runs.map((run) => (
                  <tr key={run.id}>
                    <td>{run.name}</td>
                    <td>
                      <span className="automation-status" data-status={run.status}>
                        {t(
                          (
                            {
                              queued: 'Queued',
                              running: 'Running',
                              'awaiting-user': 'Awaiting interaction',
                              completed: 'Completed',
                              failed: 'Requires review',
                              cancelled: 'Cancelled',
                            } as Record<string, string>
                          )[run.status] || run.status,
                        )}
                      </span>
                    </td>
                    <td>
                      <button
                        onClick={() =>
                          void perform(async () =>
                            setSelected(await request<Run>('/runs/' + run.id)),
                          )
                        }
                      >
                        {t('View execution')}
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {!status.runs.length && (
            <p className="automation-empty">{t('No executions in this workspace yet.')}</p>
          )}
          {selected && (
            <article className="domus-run-detail">
              <h2>{selected.name}</h2>
              {selected.message && <p>{selected.message}</p>}
              {!['completed', 'failed', 'cancelled'].includes(selected.status) && (
                <button
                  disabled={busy}
                  onClick={() =>
                    void perform(async () => {
                      await request('/runs/' + selected.id + '/cancel', {
                        method: 'POST',
                        body: '{}',
                      });
                      setSelected(await request<Run>('/runs/' + selected.id));
                    })
                  }
                >
                  {t('Cancel execution')}
                </button>
              )}
              {!!selected.files?.length && (
                <a
                  className="domus-download-button"
                  href={
                    '/automations-api/api/runs/' +
                    selected.id +
                    '/results.zip?workspace=' +
                    encodeURIComponent(workspace)
                  }
                >
                  <Download size={16} aria-hidden="true" />
                  {t('Download results ZIP')}
                </a>
              )}
              <details>
                <summary>{t('Execution log')}</summary>
                <pre>{selected.logs?.join('\n')}</pre>
              </details>
            </article>
          )}
        </>
      )}
      {tab === 'Automation settings' && canManagePlatform && (
        <form
          className="automation-panel automation-settings-panel"
          onSubmit={(event) => {
            event.preventDefault();
            void perform(async () => {
              await request('/settings/github', {
                method: 'POST',
                body: JSON.stringify({ repository, token: token || undefined }),
              });
              setToken('');
              const value = await request<{ saved: boolean }>('/settings/github');
              setSaved(value.saved);
            });
          }}
        >
          <h2>{t('Automation access')}</h2>
          <p>
            {t('Shared configuration for the platform. Tokens are encrypted and never returned.')}
          </p>
          <label>
            {t('GitHub repository')}
            <input
              required
              value={repository}
              onChange={(event) => setRepository(event.target.value)}
              placeholder="owner/repository"
            />
          </label>
          <label>
            {t('Personal token (optional)')}
            <input
              type="password"
              autoComplete="off"
              value={token}
              onChange={(event) => setToken(event.target.value)}
              placeholder={saved ? t('Token saved; leave empty to keep it') : 'github_pat_…'}
            />
          </label>
          <div className="automation-settings-actions">
            <button className="automation-primary" disabled={busy}>
              {t('Save settings')}
            </button>
            {saved && (
              <button
                type="button"
                disabled={busy}
                onClick={() =>
                  void perform(async () => {
                    await request('/settings/github/remove', { method: 'POST', body: '{}' });
                    setSaved(false);
                    setToken('');
                  })
                }
              >
                {t('Remove token')}
              </button>
            )}
          </div>
        </form>
      )}
    </section>
  );
}
