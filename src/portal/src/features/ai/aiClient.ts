import { readJsonResponse } from '../../infrastructure/jsonResponse';
export type Connection = {
  configured: boolean;
  connected: boolean;
  email: string | null;
  model: string;
  provider: string;
};
export type AiModel = { id: string; name: string };
export type WorkspaceAi = {
  configuration: { enabled: boolean; provider: string };
  canManage: boolean;
  usage: { month: string; requests: number; inputTokens: number; outputTokens: number };
};
export async function aiRequest<T>(
  workspace: string,
  path: string,
  init: RequestInit = {},
): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set('X-Domus-Workspace', workspace);
  headers.set('X-Domus-Request', '1');
  if (init.body) headers.set('Content-Type', 'application/json');
  const response = await fetch('/ai-api/api' + path, { ...init, headers, credentials: 'include' });
  return readJsonResponse<T>(
    response,
    'O serviço de IA está indisponível. Tente novamente mais tarde.',
  );
}
export const providerName = (id: string) =>
  ({ openai: 'ChatGPT', claude: 'Claude', gemini: 'Gemini' })[id] ?? id;
