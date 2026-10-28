import type { ChatResponse } from './api';
import type { BotReply } from './botReply';

/** Datos de un turno del router, para el panel de diagnóstico. */
export interface Turn {
  /** Texto enviado realmente a la API (puede incluir mensajes anteriores tras un Fallback). */
  sent: string;
  response: ChatResponse;
  reply: BotReply;
  /** Reconducciones hechas antes de este turno. */
  redirectsBefore: number;
  elapsedMs: number;
}

export type ChatItem =
  | { id: number; kind: 'user'; text: string }
  | { id: number; kind: 'bot'; turn: Turn }
  | { id: number; kind: 'error'; text: string };
