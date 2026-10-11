import { ArrowUpRight, MessageSquare, MessageSquarePlus, Send, Sparkles, X } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import type { Project } from '../../domain/models';
import { isActivitiesProject } from '../../domain/projectRules';
import { useI18n } from '../../shared/i18n';
import { aiRequest, type Connection, type WorkspaceAi } from './aiClient';
import './ai.css';
import { AssistantText } from './AssistantText';
type Reference = { id: string; title: string; project: string; href: string };
type Answer = {
  conversationId: string;
  answer: string;
  references: Reference[];
  cardsIncluded: number;
  totalCards: number;
  model: string;
  actions?: { id: string; tool: string; arguments: Record<string, unknown> }[];
};
type Turn = { question: string; result: Answer };
const actionLabels: Record<string, string> = {
  create_card: 'Create card',
  change_workspace_item: 'Update workspace',
  update_my_profile: 'Update profile',
  install_automation: 'Install automation',
  start_automation_run: 'Start automation',
  cancel_automation_run: 'Cancel execution',
  set_workspace_ai_enabled: 'AI settings',
  set_my_ai_model: 'ChatGPT model',
};
function actionFields(argumentsValue: Record<string, unknown>) {
  const fields = { ...argumentsValue };
  for (const key of ['fieldsJson', 'valuesJson']) {
    if (typeof fields[key] === 'string') {
      try {
        Object.assign(fields, JSON.parse(fields[key] as string));
      } catch {
        /* Display the original value when parsing fails. */
      }
      delete fields[key];
    }
  }
  delete fields.expectedVersion;
  return Object.entries(fields).filter(([, value]) => value !== null && value !== '');
}

export function ProjectAssistant({
  workspace,
  mode = 'project',
  projects,
  initialProject,
  open,
  onClose,
  onSettings,
  onCard,
  onModeChange,
  minimal = false,
  initialQuestion = '',
  onAction,
}: {
  workspace: string;
  mode?: 'project' | 'automations' | 'workspace';
  minimal?: boolean;
  initialQuestion?: string;
  onAction?: () => Promise<void>;
  projects: Project[];
  initialProject: string;
  open: boolean;
  onClose: () => void;
  onSettings: () => void;
  onModeChange?: (mode: 'project' | 'automations') => void;
  onCard: (id: string) => Promise<void>;
}) {
  const { t, locale } = useI18n();
  const [projectId, setProjectId] = useState(initialProject);
  const [question, setQuestion] = useState(initialQuestion);
  const [turns, setTurns] = useState<Turn[]>([]);
  const [ready, setReady] = useState<boolean | null>(null);
  const [busy, setBusy] = useState(false);
  const [pendingQuestion, setPendingQuestion] = useState('');
  const [actionResults, setActionResults] = useState<Record<string, string>>({});
  const [acting, setActing] = useState<string | null>(null);
  const [error, setError] = useState('');
  const controller = useRef<AbortController | null>(null);
  const panel = useRef<HTMLElement>(null);
  const messages = useRef<HTMLDivElement>(null);
  useEffect(() => {
    if (!open) return;
    let active = true;
    setReady(null);
    setError('');
    void Promise.all([
      aiRequest<WorkspaceAi>(workspace, '/workspace'),
      aiRequest<Connection>(workspace, '/connection'),
    ])
      .then(([settings, account]) => {
        if (active)
          setReady(settings.configuration.enabled && account.connected && !!account.model);
      })
      .catch((reason) => {
        if (active) {
          setReady(false);
          setError(reason instanceof Error ? reason.message : String(reason));
        }
      });
    const previous = document.activeElement as HTMLElement | null;
    const overflow = document.body.style.overflow;
    document.body.style.overflow = 'hidden';
    panel.current?.querySelector<HTMLButtonElement>('button')?.focus();
    return () => {
      active = false;
      controller.current?.abort();
      setBusy(false);
      setPendingQuestion('');
      document.body.style.overflow = overflow;
      previous?.focus();
    };
  }, [open, workspace]);
  useEffect(() => {
    messages.current?.scrollTo({ top: messages.current.scrollHeight, behavior: 'smooth' });
  }, [turns, busy, pendingQuestion]);
  const reset = () => {
    controller.current?.abort();
    setBusy(false);
    setTurns([]);
    setActionResults({});
    setPendingQuestion('');
    setQuestion('');
    setError('');
  };
  const send = async (text: string) => {
    if (!ready || busy || !text.trim()) return;
    const current = new AbortController();
    controller.current = current;
    setBusy(true);
    setPendingQuestion(text.trim());
    setQuestion('');
    setError('');
    try {
      const result = await aiRequest<Answer>(workspace, '/assistant', {
        method: 'POST',
        signal: current.signal,
        body: JSON.stringify({
          projectId: mode === 'automations' ? 'my-activities' : projectId,
          scope: mode,
          question: text.trim(),
          locale,
          conversationId: turns.at(-1)?.result.conversationId ?? null,
        }),
      });
      if (!current.signal.aborted) {
        setTurns((previous) => [...previous, { question: text.trim(), result }].slice(-6));
      }
    } catch (reason) {
      if (!current.signal.aborted) {
        setQuestion(text.trim());
        setError(reason instanceof Error ? reason.message : String(reason));
      }
    } finally {
      if (controller.current === current) {
        setBusy(false);
        setPendingQuestion('');
      }
    }
  };
  useEffect(() => {
    if (open && minimal) {
      setQuestion(initialQuestion);
      requestAnimationFrame(() => {
        const input = panel.current?.querySelector('textarea');
        input?.focus();
        input?.setSelectionRange(initialQuestion.length, initialQuestion.length);
      });
    }
  }, [open, minimal, initialQuestion]);
  if (!open) return null;
  return (
    <div className="modal-overlay card-drawer-overlay assistant-overlay" onClick={onClose}>
      <section
        ref={panel}
        className={`modal card-drawer assistant-panel ${minimal ? 'assistant-conversation' : ''}`}
        role="dialog"
        aria-modal="true"
        aria-label={t(
          minimal || onModeChange
            ? 'Assistant'
            : mode === 'automations'
              ? 'Automation assistant'
              : 'Project assistant',
        )}
        onClick={(event) => event.stopPropagation()}
        onKeyDown={(event) => {
          if (event.key === 'Escape') {
            event.stopPropagation();
            onClose();
          }
          if (event.key !== 'Tab') return;
          const focusable = Array.from(
            panel.current?.querySelectorAll<HTMLElement>(
              'button:not(:disabled), a[href], select:not(:disabled), textarea:not(:disabled)',
            ) ?? [],
          );
          const first = focusable[0],
            last = focusable.at(-1);
          if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last?.focus();
          } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first?.focus();
          }
        }}
      >
        <div className="modal-heading assistant-heading">
          <h2>
            <Sparkles size={19} />
            {t(
              minimal || onModeChange
                ? 'Assistant'
                : mode === 'automations'
                  ? 'Automation assistant'
                  : 'Project assistant',
            )}
          </h2>
          {minimal && (
            <button
              type="button"
              className="icon-button"
              aria-label={t('New conversation')}
              title={t('New conversation')}
              disabled={!turns.length && !busy}
              onClick={reset}
            >
              <MessageSquarePlus size={18} />
            </button>
          )}
          <button type="button" className="icon-button" aria-label={t('Close')} onClick={onClose}>
            <X size={18} />
          </button>
        </div>
        {!minimal && onModeChange && (
          <div className="assistant-area-switch" role="group" aria-label={t('Assistant area')}>
            {(['project', 'automations'] as const).map((area) => (
              <button
                key={area}
                type="button"
                aria-pressed={mode === area}
                disabled={busy}
                onClick={() => onModeChange(area)}
              >
                {t(area === 'project' ? 'Projects' : 'Automations')}
              </button>
            ))}
          </div>
        )}
        {!minimal && (
          <div className="assistant-scope">
            {mode === 'project' && (
              <label>
                {t('Project')}
                <select
                  aria-label={t('Assistant project')}
                  value={projectId}
                  onChange={(event) => {
                    reset();
                    setProjectId(event.target.value);
                  }}
                >
                  {projects
                    .filter((project) => !project.archived)
                    .map((project) => (
                      <option key={project.id} value={project.id}>
                        {isActivitiesProject(project) ? t(project.name) : project.name}
                      </option>
                    ))}
                </select>
              </label>
            )}
            <button
              type="button"
              className="secondary assistant-new-conversation"
              disabled={!turns.length && !busy}
              onClick={reset}
            >
              <MessageSquarePlus size={16} aria-hidden="true" />
              {t('New conversation')}
            </button>
          </div>
        )}
        <div ref={messages} className="assistant-messages" aria-live="polite" aria-busy={busy}>
          {!minimal && !turns.length && !pendingQuestion && (
            <div className="assistant-empty">
              <MessageSquare size={30} />
              <h3>
                {t(
                  mode === 'automations'
                    ? 'Your automations, at a glance'
                    : 'A clearer view of your project',
                )}
              </h3>
              <p>
                {t(
                  mode === 'automations'
                    ? 'Ask about available automations, installations and execution results.'
                    : 'Ask about progress, priorities or blockers. Answers use the current project cards.',
                )}
              </p>
              <div className="assistant-starters">
                {(mode === 'automations'
                  ? [
                      'Which automations are installed?',
                      'Are there any failed executions?',
                      'What automations are available?',
                    ]
                  : ['Summarize progress', 'What is overdue?', 'Where are the blockers?']
                ).map((prompt) => (
                  <button
                    key={prompt}
                    type="button"
                    className="secondary"
                    disabled={!ready || busy}
                    onClick={() => void send(t(prompt))}
                  >
                    {t(prompt)}
                    <ArrowUpRight size={14} />
                  </button>
                ))}
              </div>
            </div>
          )}
          {ready === null && <p className="assistant-note">{t('Loading…')}</p>}
          {ready === false && (
            <div className="assistant-setup">
              <p>
                {t('Connect ChatGPT, choose a model and enable AI for this workspace to start.')}
              </p>
              <button type="button" className="secondary" onClick={onSettings}>
                {t('AI settings')}
              </button>
            </div>
          )}
          {turns.map((turn, index) => (
            <article className="assistant-turn" key={index}>
              <p className="assistant-question">{turn.question}</p>
              <div className="assistant-answer">
                <AssistantText text={turn.result.answer} />
                {turn.result.references.length > 0 && (
                  <div className="assistant-sources">
                    <small>{t('Source cards')}</small>
                    {turn.result.references.map((reference) => (
                      <a
                        key={reference.id}
                        href={reference.href}
                        onClick={(event) => {
                          if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey)
                            return;
                          event.preventDefault();
                          void onCard(reference.id).catch((reason) =>
                            setError(reason instanceof Error ? reason.message : String(reason)),
                          );
                        }}
                      >
                        <span>{reference.title}</span>
                        <ArrowUpRight size={14} />
                      </a>
                    ))}
                  </div>
                )}
                {turn.result.actions?.map((action) => (
                  <div className="assistant-action" key={action.id}>
                    <strong>{t(actionLabels[action.tool] ?? 'Confirm action')}</strong>
                    <dl>
                      {actionFields(action.arguments).map(([name, value]) => (
                        <div key={name}>
                          <dt>{t(name.charAt(0).toUpperCase() + name.slice(1))}</dt>
                          <dd>
                            {typeof value === 'object' ? JSON.stringify(value) : String(value)}
                          </dd>
                        </div>
                      ))}
                    </dl>
                    {actionResults[action.id] ? (
                      <p role="status">{actionResults[action.id]}</p>
                    ) : (
                      <button
                        type="button"
                        className="primary"
                        disabled={acting !== null}
                        onClick={async () => {
                          setActing(action.id);
                          try {
                            await aiRequest(workspace, `/assistant/actions/${action.id}`, {
                              method: 'POST',
                              body: '{}',
                            });
                            setActionResults((previous) => ({
                              ...previous,
                              [action.id]: t('Action completed'),
                            }));
                            await onAction?.().catch(() =>
                              setError(t('Action completed. Refresh the page to see the update.')),
                            );
                          } catch (reason) {
                            setActionResults((previous) => ({
                              ...previous,
                              [action.id]:
                                reason instanceof Error ? reason.message : String(reason),
                            }));
                          } finally {
                            setActing(null);
                          }
                        }}
                      >
                        {t('Confirm action')}
                      </button>
                    )}
                  </div>
                ))}
                <small className="assistant-note">ChatGPT · {turn.result.model}</small>
                {turn.result.cardsIncluded < turn.result.totalCards && (
                  <small className="assistant-note">
                    {t(
                      'Consulted {0} cards through project search.',
                      turn.result.cardsIncluded,
                      turn.result.totalCards,
                    )}
                  </small>
                )}
              </div>
            </article>
          ))}
          {pendingQuestion && <p className="assistant-question">{pendingQuestion}</p>}
          {busy && (
            <p className="assistant-note" role="status">
              {t('Thinking…')}
            </p>
          )}
          {error && <p role="alert">{error}</p>}
        </div>
        <form
          className="assistant-composer"
          onSubmit={(event) => {
            event.preventDefault();
            void send(question);
          }}
        >
          <label className="assistant-label" htmlFor="assistant-question">
            {t(
              minimal
                ? 'Ask the assistant'
                : mode === 'automations'
                  ? 'Ask about these automations'
                  : 'Ask about this project',
            )}
          </label>
          <textarea
            id="assistant-question"
            placeholder={t(
              minimal
                ? 'Ask the assistant…'
                : mode === 'automations'
                  ? 'Ask about these automations…'
                  : 'Ask about this project…',
            )}
            value={question}
            maxLength={2000}
            disabled={busy}
            onChange={(event) => setQuestion(event.target.value)}
            onKeyDown={(event) => {
              if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) {
                event.preventDefault();
                void send(question);
              }
            }}
          />
          <div className="ai-actions">
            <small>{t('Answers may be inaccurate. Check the source cards.')}</small>
            {busy ? (
              <button
                type="button"
                className="secondary"
                onClick={() => {
                  controller.current?.abort();
                  setQuestion(pendingQuestion);
                  setPendingQuestion('');
                  setBusy(false);
                }}
              >
                {t('Cancel')}
              </button>
            ) : (
              <button type="submit" className="primary" disabled={!ready || !question.trim()}>
                <Send size={15} />
                {t('Send')}
              </button>
            )}
          </div>
        </form>
      </section>
    </div>
  );
}
