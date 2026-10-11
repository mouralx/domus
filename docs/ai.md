# AI assistance with your ChatGPT subscription

Domus uses a personal OAuth connection to an eligible ChatGPT plan. There are no API-key fields, no shared platform AI credentials and no Domus monthly request limit.

The first phase improves card descriptions, summarizes saved card text/comments and generates checklist suggestions. Review the output, apply it to the draft, then save the card normally. It does not execute automations.

## Local testing

1. Start Compose with `AI_OPENAI_OAUTH_MODE=opensource` and `DOMUS_PUBLIC_URL=http://127.0.0.1:8088`.
2. Open **http://127.0.0.1:8088**, sign in and complete the normal profile/2FA requirements. Use `127.0.0.1` for this OAuth route. In local OSS mode, the portal redirects a same-port `localhost` visit to the configured `127.0.0.1` origin before authentication. A fresh login may be needed because cookies are scoped to the hostname.
3. Open **Settings → Workspace → AI settings**.
4. Choose **Continue with ChatGPT**. Sign in to OpenAI and authorize Domus to use your eligible plan.
5. After returning, choose a model from the account-specific catalog and save it.
6. The workspace owner enables AI for the workspace. Each member connects their own account; provider credentials are never shared between members.
7. Open a saved card and try **Improve description**, **Summarize card** or **Generate checklist**.

Applying a suggestion changes the draft only. Unsaved edits disable generation, and changing the draft or saved card invalidates applying an old suggestion. Read-only cards cannot apply changes. Summaries are displayed without modifying the card.

Automated tests use synthetic identities/tokens and do not establish subscription eligibility. Local diagnostics have verified an actual subscription function-tool cycle using synthetic card data; account holders still review consent and responses using their own project data.

## OAuth registration

Domus is licensed under MIT. In `opensource` mode it uses OpenAI's documented dynamic registration flow, identifying itself as **Domus**, without a client secret or partner API key:

- Initial authorization uses `client_id=dynamic_agent_client`, the actual app name and a persistent host ID.
- OpenAI returns an issued `oaiapp_…` client ID. Domus uses that issued ID for the code exchange and retains it for later sign-ins.
- Redirect uses the HTTP loopback host `127.0.0.1` and `/auth/callback`, with the exact same URI in authorization and code exchange.
- Each attempt uses fresh state, nonce and PKCE S256 values, a ten-minute expiration and a browser-bound HttpOnly cookie.
- The callback revalidates the Domus session/workspace. It validates OpenAI's signed ID token, issuer, audience, expiration and nonce, and checks the granted plan-usage scopes.
- Reauthorization must match the existing selected ChatGPT identity. Disconnect before linking a different account.
- Token refreshes are serialized per account. Rotating refresh tokens are saved atomically, and a stale refresh cannot recreate a disconnected connection.

An optional registered-client route exists for eligible deployments with `AI_OPENAI_CLIENT_ID`. Its callback is `/ai-api/oauth/callback`; an optional confidential-client secret uses the server-only `AI_OPENAI_CLIENT_SECRET`. This is not needed for the local open-source route and must not be populated with an API key.

**Provider support:** OpenAI documents plan usage for eligible ChatGPT accounts and apps. Claude's published third-party-app guidance does not provide an equivalent general subscription flow for this Domus integration. Gemini's documented OAuth is cloud API authorization, not a promise of access using a consumer Gemini subscription. Domus currently offers subscription connection only for ChatGPT.

## Service and settings

`src/services/ai` is an independent .NET 10 API. Nginx forwards `/ai-api/` and the exact `/auth/callback` route to it. The service validates workspace access through boards and reads the saved card using the caller's cookie. It never receives arbitrary card text from the browser.

| Environment variable | Purpose |
|---|---|
| `ASPNETCORE_URLS` | Listen address, normally `http://+:4330` |
| `AI_DATA_DIR` | Protection keys and legacy file import location |
| `ConnectionStrings__Postgres` | Dedicated AI PostgreSQL connection |
| `DOMUS_MCP_URL` | Internal MCP endpoint, normally `http://mcp-server:4340/mcp` |
| `PORTAL_PUBLIC_URL` | Exact browser origin; local OSS flow requires `http://127.0.0.1:<port>` |
| `DOMUS_WORK_API_URL` | Internal boards origin for identity and saved-card access |
| `AI_OPENAI_OAUTH_MODE` | `opensource` for dynamic, loopback registration |
| `AI_OPENAI_CLIENT_ID` | Optional pre-registered eligible client, outside OSS mode |
| `AI_OPENAI_CLIENT_SECRET` | Optional confidential OAuth client secret, outside OSS mode |

Compose includes the service and the persistent `ai-data` volume. No AI provider keys are required. The local `infra/.env` is ignored by Git. For Vite, set `AI_PROXY_TARGET` when the service is not at `http://127.0.0.1:4330`; the configured public origin must match the browser URL.

## Context and inference

For card suggestions, only the saved card's title, description, comments and checklist are sent externally. Attachments, history and platform credentials are excluded. Context is limited to 20,000 characters. Treat card text as untrusted data; the model has no shell, SQL or action tools.

Inference uses the user's OAuth bearer credential with the public Responses API. Requests use `store: false`, `stream: true`, an array of input messages and structured output. Unsupported subscription-route fields such as `max_output_tokens` and `temperature` are omitted. Domus treats a request as successful only after the terminal `response.completed` event, and handles streamed subscription-limit failures and incomplete streams.

Users can enter sensitive information in card text. Review that content before requesting assistance. `store: false` is not a general zero-retention guarantee; the selected provider's account terms still apply.

## Storage and controls

OAuth credentials are encrypted with ASP.NET Data Protection and bound to the Domus user ID. The UI receives only connection metadata and model choices. Tokens, authorization codes, prompts and suggestions are not included in operational logs. Gateway access logs are disabled for AI OAuth callback paths.

Keep the entire data volume, including its protection keys, private and backed up. Protection keys have filesystem protection, not an external KMS. The initial atomic JSON store supports **one service instance per volume**; a transactional store is required before horizontal scaling.

Domus imposes no monthly request or monetary cap. The provider's subscription eligibility, model access and usage limits govern inference. Short-term controls remain: ten generation starts per user/workspace per minute, three simultaneous generations per workspace, bounded response size and a 90-second timeout. These prevent accidental repeated or runaway requests.

Current-month usage records contain actor/workspace IDs, model, action, status, timestamps and reported token counts. Usage metadata is stored in the AI database; no prompts or answers are persisted there. Failed/cancelled requests are recorded; no monthly quota is enforced. Disconnect removes credentials from Domus and cancels pending connection attempts; users can also revoke the provider grant through ChatGPT's app-access settings.

## Validation

```sh
dotnet run --project tests/Ai.Tests/Domus.Ai.Tests.csproj
node tests/ai-api.mjs
PLAYWRIGHT_CHANNEL=chrome node tests/ai-browser.mjs
python3 -m unittest discover -s tests -p 'test_image_*.py'
```

Tests cover personal encrypted credentials, access isolation, more than 100 requests without a monthly cap, PKCE, state/code replay, ID-token signature/nonce, dynamic issued-client handling, stable host IDs, rotating refresh/disconnect races and the subscription SSE request contract. API/browser tests cover permission checks, reviewed edits and the absence of API-key/monthly-limit fields.

## Homelab

The new image is `ghcr.io/mouralx/domus-ai`; the MCP image is `ghcr.io/mouralx/domus-mcp`. Image selection follows changed components and their build dependencies. A separate homelab Compose must add AI and MCP services, the AI database, protection-key volume and internal network configuration before using this architecture.

A loopback callback reaches the computer running the browser, not a remote homelab server. Local OSS sign-in is followed by the protected connection-transfer flow for self-hosted servers:

1. In local AI settings, connect ChatGPT and choose a model.
2. Expand **Move connection to another Domus**, enter the server’s HTTPS origin and choose **Export and disconnect here**.
3. Keep the encrypted JSON download and generated transfer code separate.
4. In the server’s AI settings, upload the protected file and enter its code within 30 minutes.
5. The server validates the transfer destination and expiry, obtains a live refreshed grant, verifies the OpenAI-signed identity, and stores the connection encrypted for the currently authenticated Domus member. Choose a model if needed and enable workspace AI.

Export clears the source connection atomically so only the destination rotates this session’s refresh tokens. Import preserves the destination host ID and records a consumed transfer ID atomically with the connection, preventing replay across Domus members. The transfer is never a model/MCP tool. Both endpoints require the normal session, workspace access, same-origin CSRF header and rate limit. No API key or monthly cap is introduced. An approved registered-client deployment can still use its exact registered callback. Existing confidential OAuth clients with a client secret are not portable through this public-client flow.

## Official references

- [OpenAI — Sign in with ChatGPT quickstart](https://developers.openai.com/siwc/quickstart)
- [OpenAI — Self-hosted VM credential transfer](https://developers.openai.com/siwc/token-sharing-open-source/self-hosted-vms)
- [OpenAI — Open-source registration and sign-in](https://developers.openai.com/siwc/token-sharing-open-source/sign-in)
- [OpenAI — Models and inference](https://developers.openai.com/siwc/token-sharing-open-source/models-and-inference)
- [OpenAI — Preview limitations](https://developers.openai.com/siwc/token-sharing-open-source/preview-limitations)
- [OpenAI — Self-hosted VMs](https://developers.openai.com/siwc/token-sharing-open-source/self-hosted-vms)
- [Claude — Authentication and third-party-app guidance](https://support.claude.com/en/articles/13189465-log-in-to-your-claude-account)
- [Gemini — OAuth authentication](https://ai.google.dev/gemini-api/docs/oauth)

## Phase 2: project assistant with MCP

Click the sparkle button in the top bar to open a small prompt. Start typing to move the draft into a conversation-only sidebar. Enter sends; Shift+Enter adds a line. The workspace assistant searches across projects and automations using authenticated MCP tools, bounded pages and exact aggregate metrics. It does not send the full workspace to the model. Source references are validated against tool results and current access.

The AI API owns personal OAuth, configuration and usage metadata in its dedicated PostgreSQL database. The independent MCP server delegates each operation to its owning domain API with the current member session. No API keys or monthly caps are introduced. Conversations expire after 30 minutes.

### Reviewed workspace actions

The assistant can prepare changes to cards, projects, members, statuses, buckets, labels, swimlanes, the current profile, automation installations/executions and AI settings. Mutation tools return a proposal rather than execute it. The conversation shows the proposed fields and a **Confirm action** button. Confirmation rechecks the current session/workspace permissions, executes the server-stored arguments once and records the result in the conversation. Proposals expire after 30 minutes and are bound to their actor, workspace and conversation. Changed workspace versions are rejected rather than overwritten. A proposal or queued automation is not completed work. Unsupported operations are reported honestly; credentials and interactive authentication are not model tools.

The same conversation can inspect complete automation manifests, execution history, logs and result-file chunks. Workers stay behind their owning domain API. See [architecture, bounds and verification](ai-architecture.md).
