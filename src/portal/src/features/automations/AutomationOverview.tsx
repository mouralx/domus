import { useEffect, useState } from 'react';
import { readJsonResponse } from '../../infrastructure/jsonResponse';
import { useI18n } from '../../shared/i18n';
type Snapshot = {
  packages: unknown[];
  metrics: { running: number; completed: number };
  runs: { id: string; name: string; status: string; startedAt: string }[];
};
export function AutomationOverview({
  workspace,
  onOpen,
}: {
  workspace: string;
  onOpen: (id: string) => void;
}) {
  const { t, locale } = useI18n();
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [error, setError] = useState('');
  useEffect(() => {
    const controller = new AbortController();
    let busy = false;
    const load = async () => {
      if (busy) return;
      busy = true;
      try {
        const response = await fetch('/automations-api/api/status', {
          headers: { 'X-Domus-Workspace': workspace },
          credentials: 'include',
          signal: controller.signal,
        });
        const body = await readJsonResponse<Snapshot>(
          response,
          t('The automation service is unavailable. Please try again later.'),
        );
        if (!controller.signal.aborted) {
          setSnapshot(body);
          setError('');
        }
      } catch (reason) {
        if (!controller.signal.aborted)
          setError(reason instanceof Error ? reason.message : String(reason));
      } finally {
        busy = false;
      }
    };
    void load();
    const timer = window.setInterval(() => void load(), 5000);
    return () => {
      controller.abort();
      window.clearInterval(timer);
    };
  }, [workspace, t]);
  return (
    <section
      className="domus-automations domus-automation-overview"
      aria-label={t('Automation overview')}
    >
      <h2 className="section-title">{t('Automation executions')}</h2>
      {error && (
        <p role="alert" className="domus-error">
          {error}
        </p>
      )}
      {!snapshot && !error && <p>{t('Loading automation executions…')}</p>}
      {snapshot && (
        <>
          <div className="domus-metrics">
            <div>
              {t('Installed flows')}
              <strong>{snapshot.packages.length}</strong>
            </div>
            <div>
              {t('Running executions')}
              <strong>{snapshot.metrics.running}</strong>
            </div>
            <div>
              {t('Completed executions')}
              <strong>{snapshot.metrics.completed}</strong>
            </div>
          </div>
          <h3>{t('Recent executions')}</h3>
          {snapshot.runs.length ? (
            <div className="automation-table-scroll">
              <table>
                <thead>
                  <tr>
                    <th>{t('Automation')}</th>
                    <th>{t('Started')}</th>
                    <th>{t('Status')}</th>
                    <th>{t('Action')}</th>
                  </tr>
                </thead>
                <tbody>
                  {snapshot.runs.slice(0, 5).map((run) => (
                    <tr key={run.id}>
                      <td>{run.name}</td>
                      <td>{new Date(run.startedAt).toLocaleString(locale)}</td>
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
                        <button onClick={() => onOpen(run.id)}>{t('View execution')}</button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          ) : (
            <p>{t('No executions in this workspace yet.')}</p>
          )}
        </>
      )}
    </section>
  );
}
