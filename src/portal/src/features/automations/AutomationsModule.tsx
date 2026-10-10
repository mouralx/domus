import { readJsonResponse } from '../../infrastructure/jsonResponse';
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
  name: string;
  description: string;
  version: string;
  installedVersion?: string;
  action: 'install' | 'update' | 'installed';
};
type Job = { id: string; status: string; error?: string };

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
  const pendingJob = useRef(false);
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
        'O serviço de automações está indisponível. Tente novamente mais tarde.',
      );
    },
    [workspace],
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
  const pkg = status?.packages.find((item) => item.key === packageKey);
  const text = (value: string) =>
    value
      .normalize('NFD')
      .replace(/\p{Diacritic}/gu, '')
      .toLowerCase();
  const pending = jobs.some((job) => ['queued', 'installing'].includes(job.status));
  return (
    <section className="domus-automations">
      {error && (
        <p className="domus-error" role="alert">
          {error}
        </p>
      )}
      {!status && !error && <p>A carregar automações…</p>}
      {tab === 'Automations' && status && (
        <>
          <table>
            <thead>
              <tr>
                <th>Automação</th>
                <th>Versão</th>
                <th>Ações</th>
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
                    {canManage && (
                      <button
                        disabled={busy}
                        onClick={() => {
                          if (
                            confirm(
                              'Remover esta automação? O histórico e os resultados serão preservados.',
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
                        Remover
                      </button>
                    )}{' '}
                    <button
                      disabled={busy || !item.compatible}
                      onClick={() => setPackageKey(item.key)}
                    >
                      Executar
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {!status.packages.length && <p>Instale a primeira automação no Catálogo.</p>}
          {pkg && (
            <form
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
                <p>O ficheiro será processado numa cópia de trabalho.</p>
              )}
              {pkg.manifest.effects?.publishesComments && (
                <p>Esta automação publica comentários na plataforma.</p>
              )}
              {pkg.manifest.effects?.createsPaidRequests && (
                <p>Esta automação pode concluir pedidos pagos.</p>
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
                      ? ` Colunas necessárias: ${input.columns.join(' · ')}.`
                      : ''}
                  </small>
                </label>
              ))}
              <button disabled={busy}>Iniciar execução</button>{' '}
              <button type="button" onClick={() => setPackageKey('')}>
                Fechar
              </button>
            </form>
          )}
        </>
      )}
      {tab === 'Catalog' && (
        <>
          <div className="domus-catalog-toolbar">
            <label>
              Pesquisar automações
              <input
                type="search"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                placeholder="Nome ou descrição"
              />
            </label>
            <button disabled={busy} onClick={() => void perform(refreshCatalog)}>
              Atualizar catálogo
            </button>
          </div>
          {pending && <p role="status">Uma instalação está em processamento pelo worker.</p>}
          {jobs
            .filter((job) => job.status === 'failed')
            .map((job) => (
              <p className="domus-error" key={job.id}>
                {job.error}
              </p>
            ))}
          <table>
            <thead>
              <tr>
                <th>Automação</th>
                <th>Disponível</th>
                <th>Instalada</th>
                <th>Ação</th>
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
                        disabled={busy || pending || !canManage || item.action === 'installed'}
                        onClick={() =>
                          void perform(async () => {
                            pendingJob.current = true;
                            await request('/catalog/install', {
                              method: 'POST',
                              body: JSON.stringify({ assetId: item.assetId }),
                            });
                          })
                        }
                      >
                        {
                          { install: 'Instalar', update: 'Atualizar', installed: 'Instalada' }[
                            item.action
                          ]
                        }
                      </button>
                    </td>
                  </tr>
                ))}
            </tbody>
          </table>
          {!catalog.length && !error && (
            <p>Não há automações disponíveis. Verifique as definições ou atualize o catálogo.</p>
          )}
        </>
      )}
      {tab === 'Executions' && status && (
        <>
          <div className="domus-metrics">
            <div>
              Pacotes instalados<strong>{status.packages.length}</strong>
            </div>
            <div>
              Execuções em curso<strong>{status.metrics.running}</strong>
            </div>
            <div>
              Execuções concluídas<strong>{status.metrics.completed}</strong>
            </div>
          </div>
          <table>
            <thead>
              <tr>
                <th>Automação</th>
                <th>Estado</th>
                <th>Ação</th>
              </tr>
            </thead>
            <tbody>
              {status.runs.map((run) => (
                <tr key={run.id}>
                  <td>{run.name}</td>
                  <td>
                    {run.status === 'failed'
                      ? 'Requer revisão'
                      : run.status === 'completed'
                        ? 'Concluída'
                        : run.status === 'cancelled'
                          ? 'Cancelada'
                          : run.status}
                  </td>
                  <td>
                    <button
                      onClick={() =>
                        void perform(async () => setSelected(await request<Run>('/runs/' + run.id)))
                      }
                    >
                      Ver execução
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          {selected && (
            <article>
              <h2>{selected.name}</h2>
              <p>{selected.message}</p>
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
                  Cancelar execução
                </button>
              )}
              {!!selected.files?.length && (
                <a
                  href={
                    '/automations-api/api/runs/' +
                    selected.id +
                    '/results.zip?workspace=' +
                    encodeURIComponent(workspace)
                  }
                >
                  Descarregar resultados ZIP
                </a>
              )}
              <details>
                <summary>Registo da execução</summary>
                <pre>{selected.logs?.join('\n')}</pre>
              </details>
            </article>
          )}
        </>
      )}
      {tab === 'Automation settings' && canManagePlatform && (
        <form
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
          <h2>Acesso às automações</h2>
          <p>
            Configuração partilhada por toda a plataforma. O token é guardado cifrado e nunca
            devolvido.
          </p>
          <label>
            Repositório GitHub
            <input
              required
              value={repository}
              onChange={(event) => setRepository(event.target.value)}
              placeholder="autor/repositorio"
            />
          </label>
          <label>
            Token pessoal (opcional)
            <input
              type="password"
              autoComplete="off"
              value={token}
              onChange={(event) => setToken(event.target.value)}
              placeholder={saved ? 'Token guardado; deixe vazio para o manter' : 'github_pat_…'}
            />
          </label>
          <button disabled={busy}>Guardar definições</button>
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
              Remover token
            </button>
          )}
        </form>
      )}
    </section>
  );
}
