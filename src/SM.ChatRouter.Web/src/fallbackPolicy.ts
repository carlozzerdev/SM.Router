import type { FallbackReason } from './api';

/**
 * Qué hace el chat ante un Fallback:
 * - reconduct: pedir al usuario que reformule (con atajos de categorías) y volver a intentarlo.
 * - retry: error técnico; ofrecer reenviar el mismo mensaje.
 * - giveUp: ofrecer el formulario de la categoría por defecto y cerrar la conversación.
 * - redirect: el usuario habla de algo ajeno al soporte de SM (o solo saluda); se le reconduce
 *   con el mensaje del modelo, sin cerrar la conversación ni contar intentos.
 */
export type FallbackAction = 'reconduct' | 'retry' | 'giveUp' | 'redirect';

/** Motivos en los que el usuario puede ayudar aportando más detalle. */
const RECOVERABLE: ReadonlySet<FallbackReason> = new Set(['LowConfidence', 'UnknownCategory']);

export function decideFallback(reason: FallbackReason | null, redirects: number, maxRedirects: number): FallbackAction {
  if (reason === 'NoMatch') {
    return 'redirect';
  }
  if (reason === 'AiError') {
    return 'retry';
  }
  if (reason !== null && RECOVERABLE.has(reason) && redirects < maxRedirects) {
    return 'reconduct';
  }
  return 'giveUp';
}

/**
 * El servidor cierra la conversación tras un Fallback y el siguiente mensaje empieza un routing
 * nuevo sin historial, así que se reenvía todo lo que el usuario ha dicho en este routing.
 */
export function composeRoutingMessage(previousUserMessages: readonly string[], newMessage: string): string {
  return [...previousUserMessages, newMessage]
    .map((m) => m.trim().replace(/[.\s]+$/, ''))
    .filter((m) => m.length > 0)
    .join('. ');
}
