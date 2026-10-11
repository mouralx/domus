import { ChevronDown, Sparkles } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import type { Task } from '../../domain/models';
import { apiEnabled } from '../../infrastructure/apiClient';
import { useI18n } from '../../shared/i18n';
import { aiRequest, providerName, type WorkspaceAi, type Connection } from './aiClient';
import './ai.css';
type Result = {
  provider: string;
  model: string;
  action: string;
  suggestion: { text: string; items: string[] };
};
const fingerprint = (task: Task) =>
  JSON.stringify([task.id, task.title, task.description, task.comments, task.checklist]);
export function CardAi({
  workspace,
  draft,
  saved,
  onApply,
}: {
  workspace: string;
  draft: Task;
  saved?: Task;
  onApply: (task: Task) => void;
}) {
  const { t, locale } = useI18n();
  const [open, setOpen] = useState(false);
  const [enabled, setEnabled] = useState(false);
  const [connected, setConnected] = useState(false);
  const [action, setAction] = useState('description');
  const [result, setResult] = useState<Result | null>(null);
  const [error, setError] = useState('');
  const [busy, setBusy] = useState(false);
  const baseline = useRef('');
  const controller = useRef<AbortController | null>(null);
  useEffect(() => {
    if (!apiEnabled) return;
    let active = true;
    void aiRequest<WorkspaceAi>(workspace, '/workspace')
      .then((value) => {
        if (active) setEnabled(value.configuration.enabled);
        return aiRequest<Connection>(workspace, '/connection');
      })
      .then((value) => {
        if (active) setConnected(value.connected && !!value.model);
      })
      .catch(() => {
        /* Settings remain available when AI is offline. */
      });
    return () => {
      active = false;
      controller.current?.abort();
    };
  }, [workspace]);
  useEffect(() => {
    setResult(null);
    setError('');
    controller.current?.abort();
  }, [draft.id]);
  if (!apiEnabled || !enabled) return null;
  const readOnly = saved?.readOnly ?? draft.readOnly ?? false;
  const changed = !saved || fingerprint(saved) !== fingerprint(draft);
  const stale = changed || baseline.current !== fingerprint(draft);
  return (
    <section className="card-ai" aria-label={t('AI assistance')}>
      <button
        type="button"
        className="secondary ai-toggle"
        aria-expanded={open}
        onClick={() => setOpen(!open)}
      >
        <Sparkles size={15} />
        {t('Help with AI')}
        <ChevronDown size={14} />
      </button>
      {open && (
        <div className="card-ai-content">
          <p>
            {t(
              'Only the saved card text is sent to the selected provider. Review the suggestion before applying it.',
            )}
          </p>
          <div className="ai-actions">
            <select
              aria-label={t('AI action')}
              value={action}
              disabled={busy}
              onChange={(event) => setAction(event.target.value)}
            >
              <option value="description">{t('Improve description')}</option>
              <option value="summary">{t('Summarize card')}</option>
              <option value="checklist">{t('Generate checklist')}</option>
            </select>
            <button
              type="button"
              className="secondary"
              disabled={busy || changed || !connected}
              onClick={async () => {
                setBusy(true);
                setError('');
                setResult(null);
                baseline.current = fingerprint(draft);
                const current = new AbortController();
                controller.current = current;
                try {
                  const value = await aiRequest<Result>(workspace, '/suggestions', {
                    method: 'POST',
                    body: JSON.stringify({ cardId: draft.id, action, locale }),
                    signal: current.signal,
                  });
                  if (!current.signal.aborted) setResult(value);
                } catch (reason) {
                  if (!current.signal.aborted)
                    setError(reason instanceof Error ? reason.message : String(reason));
                } finally {
                  if (controller.current === current) setBusy(false);
                }
              }}
            >
              <Sparkles size={15} />
              {t(busy ? 'Generating…' : 'Generate suggestion')}
            </button>
            {busy && (
              <button
                type="button"
                className="text-button"
                onClick={() => {
                  controller.current?.abort();
                  setBusy(false);
                }}
              >
                {t('Cancel')}
              </button>
            )}
          </div>
          {!connected && <small>{t('Connect ChatGPT and choose a model in AI settings.')}</small>}
          {changed && <small>{t('Save the card before requesting AI assistance.')}</small>}
          {error && <p role="alert">{error}</p>}
          {result && (
            <div className="ai-suggestion">
              <small>
                {providerName(result.provider)} · {result.model}
              </small>
              <p>{result.suggestion.text}</p>
              {!!result.suggestion.items.length && (
                <ul>
                  {result.suggestion.items.map((item, index) => (
                    <li key={index}>{item}</li>
                  ))}
                </ul>
              )}
              {stale && (
                <p role="status">
                  {t('The card changed. Generate a new suggestion before applying.')}
                </p>
              )}
              <div className="ai-actions">
                {result.action !== 'summary' && (
                  <button
                    type="button"
                    className="primary"
                    disabled={stale || readOnly}
                    onClick={() => {
                      if (stale || readOnly) return;
                      if (result.action === 'description')
                        onApply({ ...draft, description: result.suggestion.text });
                      else {
                        const existing = new Set(
                          draft.checklist.map((item) => item.text.trim().toLocaleLowerCase()),
                        );
                        const items = result.suggestion.items
                          .filter((text) => {
                            const key = text.trim().toLocaleLowerCase();
                            if (existing.has(key)) return false;
                            existing.add(key);
                            return true;
                          })
                          .map((text) => ({ text, done: false }));
                        onApply({ ...draft, checklist: [...draft.checklist, ...items] });
                      }
                      setResult(null);
                    }}
                  >
                    {t('Apply to draft')}
                  </button>
                )}
                <button type="button" className="text-button" onClick={() => setResult(null)}>
                  {t('Dismiss')}
                </button>
              </div>
              {result.action !== 'summary' && (
                <small>{t('Save the card to keep the applied suggestion.')}</small>
              )}
            </div>
          )}
        </div>
      )}
    </section>
  );
}
