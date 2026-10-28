// Espejo de SM.ChatRouter.Api/Contracts/ChatContracts.cs y Domain/Enums/Enums.cs.
// Los enums llegan como texto por el JsonStringEnumConverter de Program.cs.

export type RoutingOutcome = 'NeedsMoreInformation' | 'Routed' | 'Fallback';

export type FallbackReason =
  | 'NoMatch'
  | 'UnknownCategory'
  | 'LowConfidence'
  | 'MaxClarificationsReached'
  | 'AiError'
  | 'NoCategoriesAvailable';

export interface CategorySummary {
  id: string;
  name: string;
  /** Ruta legible N1 > N2 > N3. */
  path: string;
}

export interface ChatResponse {
  conversationId: string;
  outcome: RoutingOutcome;
  question: string | null;
  category: CategorySummary | null;
  parameters: Record<string, string>;
  missingParameters: string[];
  fallbackReason: FallbackReason | null;
}

export interface CategoryNode {
  slug: string;
  name: string;
}

/** Categoría tal como la devuelve GET /api/categories (solo los campos que usa la web). */
export interface Category {
  id: string;
  name: string;
  level1: CategoryNode | null;
  level2: CategoryNode | null;
  language: string | null;
}

/** Error HTTP de la API; `detail` sale del ProblemDetails cuando lo hay. */
export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

async function ensureOk(response: Response): Promise<Response> {
  if (response.ok) {
    return response;
  }
  let message = `${response.status} ${response.statusText}`;
  try {
    const problem = await response.json();
    const errors = problem.errors ? Object.values(problem.errors).flat().join(' ') : '';
    message = errors || problem.detail || problem.title || message;
  } catch {
    // Respuesta sin cuerpo JSON: se queda el status.
  }
  throw new ApiError(response.status, message);
}

export async function sendMessage(conversationId: string | null, message: string): Promise<ChatResponse> {
  const response = await fetch('/api/chat', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ conversationId, message }),
  });
  return (await ensureOk(response)).json();
}

export async function resetConversation(conversationId: string): Promise<void> {
  const response = await fetch(`/api/chat/${encodeURIComponent(conversationId)}`, { method: 'DELETE' });
  // 404: la conversación ya no existe en el servidor, que es lo que se quería.
  if (response.status !== 404) {
    await ensureOk(response);
  }
}

export async function getCategories(): Promise<Category[]> {
  return (await ensureOk(await fetch('/api/categories'))).json();
}
