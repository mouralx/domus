import { Copy, Download, Upload } from 'lucide-react';
import { useState } from 'react';
import { useI18n } from '../../shared/i18n';
import { aiRequest } from './aiClient';

type ProtectedTransfer = { version: number; nonce: string; ciphertext: string; tag: string };
type Exported = { file: ProtectedTransfer; code: string; email: string };
export function ConnectionTransfer({
  workspace,
  connected,
  configured,
  onChanged,
}: {
  workspace: string;
  connected: boolean;
  configured: boolean;
  onChanged: () => Promise<void>;
}) {
  const { t } = useI18n();
  const [destination, setDestination] = useState('');
  const [code, setCode] = useState('');
  const [file, setFile] = useState<ProtectedTransfer | null>(null);
  const [exported, setExported] = useState<Exported | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  const [message, setMessage] = useState('');
  const download = (value: Exported) => {
    const url = URL.createObjectURL(
      new Blob([JSON.stringify(value.file)], { type: 'application/json' }),
    );
    const link = document.createElement('a');
    link.href = url;
    link.download = 'domus-chatgpt-connection.json';
    link.click();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
  };
  return (
    <details className="ai-transfer" open={(!configured && !connected) || !!exported}>
      <summary>
        {t(
          connected || exported
            ? 'Move connection to another Domus'
            : 'Import a protected connection',
        )}
      </summary>
      <p>
        {t(
          connected
            ? 'This moves your ChatGPT connection. This installation disconnects; the destination manages future renewals. The file is encrypted and expires in 30 minutes.'
            : 'Connect ChatGPT in your local Domus, then export a protected connection for this installation. Upload the file and enter its separate transfer code.',
        )}
      </p>
      {!connected && !exported && (
        <small>
          {t('Destination address')}: {window.location.origin}
        </small>
      )}
      {error && <p role="alert">{error}</p>}
      {message && <p role="status">{message}</p>}
      {exported ? (
        <div className="ai-transfer-receipt">
          <p>
            {t(
              'Connection exported for {0}. Keep the code separate from the file.',
              exported.email,
            )}
          </p>
          <label>
            {t('Transfer code')}
            <input type="password" readOnly value={exported.code} autoComplete="off" />
          </label>
          <div className="ai-actions">
            <button
              type="button"
              className="secondary"
              onClick={() =>
                void navigator.clipboard
                  .writeText(exported.code)
                  .then(() => setMessage(t('Transfer code copied.')))
                  .catch(() => setError(t('Copy the transfer code from the field.')))
              }
            >
              <Copy size={15} />
              {t('Copy code')}
            </button>
            <button type="button" className="secondary" onClick={() => download(exported)}>
              <Download size={15} />
              {t('Download protected file')}
            </button>
          </div>
        </div>
      ) : connected ? (
        <form
          onSubmit={async (event) => {
            event.preventDefault();
            setBusy(true);
            setError('');
            setMessage('');
            try {
              const value = await aiRequest<Exported>(workspace, '/connection/export', {
                method: 'POST',
                body: JSON.stringify({ destination }),
              });
              setExported(value);
              download(value);
              await onChanged();
            } catch (reason) {
              setError(reason instanceof Error ? reason.message : String(reason));
            } finally {
              setBusy(false);
            }
          }}
        >
          <label>
            {t('Destination Domus URL')}
            <input
              type="url"
              value={destination}
              required
              placeholder="https://domus.example.com"
              disabled={busy}
              onChange={(event) => setDestination(event.target.value)}
            />
          </label>
          <button type="submit" className="secondary" disabled={busy}>
            <Download size={15} />
            {t(busy ? 'Working…' : 'Export and disconnect here')}
          </button>
        </form>
      ) : (
        <form
          onSubmit={async (event) => {
            event.preventDefault();
            if (!file) return;
            setBusy(true);
            setError('');
            setMessage('');
            try {
              await aiRequest(workspace, '/connection/import', {
                method: 'POST',
                body: JSON.stringify({ file, code: code.trim() }),
              });
              setCode('');
              setFile(null);
              setMessage(t('ChatGPT connection imported.'));
              await onChanged();
            } catch (reason) {
              setError(reason instanceof Error ? reason.message : String(reason));
            } finally {
              setBusy(false);
            }
          }}
        >
          <label>
            {t('Protected connection file')}
            <input
              type="file"
              accept="application/json,.json"
              disabled={busy}
              onChange={async (event) => {
                const selected = event.target.files?.[0];
                setFile(null);
                setError('');
                try {
                  if (!selected) return;
                  if (selected.size > 30000)
                    throw new Error(t('Invalid protected connection file.'));
                  const value = JSON.parse(await selected.text()) as ProtectedTransfer;
                  if (
                    value.version !== 1 ||
                    typeof value.nonce !== 'string' ||
                    typeof value.ciphertext !== 'string' ||
                    typeof value.tag !== 'string'
                  )
                    throw new Error(t('Invalid protected connection file.'));
                  setFile(value);
                } catch (reason) {
                  setError(reason instanceof Error ? reason.message : String(reason));
                }
              }}
            />
          </label>
          <label>
            {t('Transfer code')}
            <input
              type="password"
              value={code}
              required
              minLength={43}
              maxLength={43}
              autoComplete="off"
              disabled={busy}
              onChange={(event) => setCode(event.target.value)}
            />
          </label>
          <button
            type="submit"
            className="primary"
            disabled={busy || !file || code.trim().length !== 43}
          >
            <Upload size={15} />
            {t(busy ? 'Working…' : 'Import ChatGPT connection')}
          </button>
        </form>
      )}
    </details>
  );
}
