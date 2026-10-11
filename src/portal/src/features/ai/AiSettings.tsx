import { useEffect, useState } from 'react';
import { Sparkles } from 'lucide-react';
import { apiEnabled } from '../../infrastructure/apiClient';
import { useI18n } from '../../shared/i18n';
import { aiRequest, type AiModel, type Connection, type WorkspaceAi } from './aiClient';
import './ai.css';
export function AiSettings({ workspace }: { workspace: string }) {
  const { t } = useI18n();
  const [data, setData] = useState<WorkspaceAi | null>(null);
  const [connection, setConnection] = useState<Connection | null>(null);
  const [models, setModels] = useState<AiModel[]>([]);
  const [model, setModel] = useState('');
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (!apiEnabled) return;
    let active = true;
    void (async () => {
      try {
        const [value, account] = await Promise.all([
          aiRequest<WorkspaceAi>(workspace, '/workspace'),
          aiRequest<Connection>(workspace, '/connection'),
        ]);
        if (!active) return;
        setData({ ...value, configuration: { ...value.configuration, provider: 'openai' } });
        setConnection(account);
        setModel(account.model);
        if (account.connected) {
          const choices = await aiRequest<AiModel[]>(workspace, '/connection/models');
          if (active) setModels(choices);
        }
      } catch (reason) {
        if (active) setError(reason instanceof Error ? reason.message : String(reason));
      }
    })();
    return () => {
      active = false;
    };
  }, [workspace]);
  if (!apiEnabled) return <p>{t('AI requires the connected platform.')}</p>;
  return (
    <div className="ai-settings">
      <p>{t('Use your ChatGPT subscription for card help and project questions.')}</p>
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      {!data && !error && <p>{t('Loading…')}</p>}
      {connection && (
        <section className="ai-provider">
          <div className="ai-section-heading">
            <h3>ChatGPT</h3>
            <span className="ai-status">
              {t(connection.connected ? 'Connected' : 'Not connected')}
            </span>
          </div>
          <p>{t('Your account is personal. Only your requests use your ChatGPT plan.')}</p>
          {connection.connected ? (
            <>
              <p>
                {t('Connected account')}: {connection.email}
              </p>
              <label>
                {t('Model')}
                <select
                  aria-label={t('ChatGPT model')}
                  value={model}
                  disabled={busy}
                  onChange={(event) => setModel(event.target.value)}
                >
                  <option value="">{t('Choose a model')}</option>
                  {models.map((item) => (
                    <option key={item.id} value={item.id}>
                      {item.name}
                    </option>
                  ))}
                </select>
              </label>
              <div className="ai-actions">
                <button
                  type="button"
                  className="primary"
                  disabled={busy || !model}
                  onClick={async () => {
                    setBusy(true);
                    setError('');
                    setMessage('');
                    try {
                      await aiRequest(workspace, '/connection/model', {
                        method: 'PUT',
                        body: JSON.stringify({ model }),
                      });
                      setConnection({ ...connection, model });
                      setMessage(t('Model saved.'));
                    } catch (reason) {
                      setError(reason instanceof Error ? reason.message : String(reason));
                    } finally {
                      setBusy(false);
                    }
                  }}
                >
                  {t('Save model')}
                </button>
                <button
                  type="button"
                  className="secondary"
                  disabled={busy}
                  onClick={async () => {
                    setBusy(true);
                    setError('');
                    setMessage('');
                    try {
                      await aiRequest(workspace, '/connection', { method: 'DELETE' });
                      setConnection({ ...connection, connected: false, model: '', email: null });
                      setModels([]);
                      setModel('');
                    } catch (reason) {
                      setError(reason instanceof Error ? reason.message : String(reason));
                    } finally {
                      setBusy(false);
                    }
                  }}
                >
                  {t('Disconnect ChatGPT')}
                </button>
              </div>
            </>
          ) : (
            <button
              type="button"
              className="primary"
              disabled={busy || !connection.configured}
              onClick={async () => {
                setBusy(true);
                setError('');
                try {
                  const value = await aiRequest<{ url: string }>(workspace, '/connection/start', {
                    method: 'POST',
                    body: '{}',
                  });
                  const url = new URL(value.url);
                  if (url.origin !== 'https://auth.openai.com')
                    throw new Error(t('Invalid connection URL.'));
                  window.location.assign(url.href);
                } catch (reason) {
                  setError(reason instanceof Error ? reason.message : String(reason));
                  setBusy(false);
                }
              }}
            >
              <Sparkles size={16} />
              {t('Continue with ChatGPT')}
            </button>
          )}
          {!connection.configured && (
            <p role="status">
              {t(
                'ChatGPT connection requires eligible OAuth client registration for Domus. Server setup is pending.',
              )}
            </p>
          )}
          <small>{t('Your ChatGPT plan applies. Domus has no monthly limit.')}</small>
        </section>
      )}
      {data && (
        <form
          onSubmit={async (event) => {
            event.preventDefault();
            setBusy(true);
            setError('');
            setMessage('');
            try {
              await aiRequest(workspace, '/workspace', {
                method: 'PUT',
                body: JSON.stringify(data.configuration),
              });
              setMessage(t('AI settings saved.'));
            } catch (reason) {
              setError(reason instanceof Error ? reason.message : String(reason));
            } finally {
              setBusy(false);
            }
          }}
        >
          <h3 className="ai-workspace-heading">{t('Workspace access')}</h3>
          <fieldset disabled={busy || !data.canManage}>
            <label className="ai-check">
              <input
                type="checkbox"
                checked={data.configuration.enabled}
                onChange={(event) =>
                  setData({
                    ...data,
                    configuration: { enabled: event.target.checked, provider: 'openai' },
                  })
                }
              />
              {t('Enable AI in this workspace')}
            </label>
            {data.canManage && (
              <button className="primary" type="submit">
                {t('Save AI settings')}
              </button>
            )}
          </fieldset>
          <small>
            {t(
              'Each member connects their own ChatGPT account. Connections are never shared with other members.',
            )}
          </small>
        </form>
      )}
      <section className="ai-provider">
        <h3>Claude · Gemini</h3>
        <p>{t('Subscription sign-in for these providers is not currently available in Domus.')}</p>
      </section>
    </div>
  );
}
