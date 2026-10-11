import React from 'react';
import { createRoot } from 'react-dom/client';
import { apiEnabled } from './infrastructure/apiClient';
import { App } from './app/App';
import { AuthBoundary } from './features/auth/AuthBoundary';
import { SharedCardRouter } from './features/cards/SharedCard';
import { I18nProvider } from './shared/i18n';
import { ThemeProvider } from './shared/Theme';
import { PlatformBrandingProvider } from './shared/PlatformBranding';
import './styles/dark.css';
import './styles/styles.css';
import './styles/branding.css';
import './styles/theme-components.css';
import './styles/platform-layout.css';
import './styles/refinement.css';
function renderApp() {
  createRoot(document.getElementById('root')!).render(
    <React.StrictMode>
      <I18nProvider>
        <PlatformBrandingProvider>
          <ThemeProvider>
            <AuthBoundary>
              <SharedCardRouter>
                <App />
              </SharedCardRouter>
            </AuthBoundary>
          </ThemeProvider>
        </PlatformBrandingProvider>
      </I18nProvider>
    </React.StrictMode>,
  );
}

async function start() {
  if (apiEnabled && window.location.hostname === 'localhost') {
    try {
      const response = await fetch('/ai-api/local-origin', { signal: AbortSignal.timeout(2000) });
      if (response.ok && response.headers.get('Content-Type')?.includes('application/json')) {
        const value: { origin?: string } = await response.json();
        if (value.origin) {
          const origin = new URL(value.origin);
          if (
            origin.protocol === 'http:' &&
            origin.hostname === '127.0.0.1' &&
            origin.port === window.location.port
          ) {
            window.location.replace(
              origin.origin +
                window.location.pathname +
                window.location.search +
                window.location.hash,
            );
            return;
          }
        }
      }
    } catch {
      // An unavailable optional AI service must not prevent the portal from loading.
    }
  }
  renderApp();
}
void start();
