import { useState } from 'react';
import { apiRequest } from '../../infrastructure/apiClient';
import { useI18n } from '../../shared/i18n';
import { Brand } from '../../shared/Brand';
import { TwoFactorSettings } from '../profile/TwoFactorSettings';

export function TwoFactorGate({
  setup,
  onComplete,
  onLogout,
}: {
  setup: boolean;
  onComplete: () => void;
  onLogout: () => void;
}) {
  const { t } = useI18n();
  const [code, setCode] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState('');
  return (
    <main className="enrollment-page">
      <section
        className="enrollment-card verification-enrollment"
        aria-labelledby="verification-title"
      >
        <div className="enrollment-brand">
          <Brand />
        </div>
        <h1 id="verification-title">{t(setup ? 'Secure your account' : 'Verify your sign-in')}</h1>
        <p>
          {t(
            setup
              ? 'Set up two-factor authentication to finish registration and open your workspace.'
              : 'Enter a code from your authenticator app, or one of your saved recovery codes.',
          )}
        </p>
        {setup ? (
          <TwoFactorSettings onComplete={onComplete} />
        ) : (
          <form
            className="two-factor-form"
            onSubmit={async (event) => {
              event.preventDefault();
              if (busy) return;
              setBusy(true);
              setError('');
              try {
                await apiRequest('/auth/two-factor/verify', {
                  method: 'POST',
                  body: JSON.stringify({ password: '', code }),
                });
                setCode('');
                onComplete();
              } catch (e) {
                setError(e instanceof Error ? e.message : 'Could not sign in. Please try again.');
              } finally {
                setBusy(false);
              }
            }}
          >
            <label>
              {t('Authenticator or recovery code')}
              <input
                autoComplete="one-time-code"
                autoFocus
                required
                maxLength={100}
                value={code}
                onChange={(e) => setCode(e.target.value)}
              />
            </label>
            <button className="primary" disabled={busy || !code.trim()}>
              {t(busy ? 'Working…' : 'Verify your sign-in')}
            </button>
          </form>
        )}
        {error && (
          <p role="alert" className="form-error">
            {t(error)}
          </p>
        )}
        <button type="button" className="secondary enrollment-signout" onClick={onLogout}>
          {t('Sign out')}
        </button>
      </section>
    </main>
  );
}
