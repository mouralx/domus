/** Keep gateway HTML and empty error responses out of user-facing API errors. */
export async function readJsonResponse<T>(response: Response, unavailable: string): Promise<T> {
  const contentType = response.headers.get('Content-Type') ?? '';
  if (!/\bapplication\/(?:[\w.-]+\+)?json\b/i.test(contentType)) {
    throw new Error(unavailable);
  }
  let body: T & { error?: string; detail?: string; title?: string };
  try {
    body = await response.json();
  } catch {
    throw new Error(unavailable);
  }
  if (!response.ok) {
    throw new Error(body?.error || body?.detail || body?.title || unavailable);
  }
  return body;
}
