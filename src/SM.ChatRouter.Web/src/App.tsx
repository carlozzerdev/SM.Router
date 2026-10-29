import { useEffect, useRef, useState, type FormEvent } from 'react';
import { ApiError, getCategories, resetConversation, sendMessage } from './api';
import { buildBotReply, config } from './botReply';
import { composeRoutingMessage } from './fallbackPolicy';
import { BotMessage } from './components/BotMessage';
import { Diagnostics } from './components/Diagnostics';
import type { ChatItem, Turn } from './types';

const WELCOME = 'Hola, cuéntame qué necesitas y te indico el formulario adecuado.';

let nextId = 1;

export default function App() {
  const [items, setItems] = useState<ChatItem[]>([]);
  const [input, setInput] = useState('');
  const [busy, setBusy] = useState(false);
  const [shortcuts, setShortcuts] = useState<string[]>([]);
  const [selectedId, setSelectedId] = useState<number | null>(null);

  // Estado de la conversación con el router.
  const [conversationId, setConversationId] = useState<string | null>(null);
  const [closed, setClosed] = useState(false);
  const [redirects, setRedirects] = useState(0);
  /** Mensajes del usuario que el servidor tiene en el routing en curso. */
  const [routingMessages, setRoutingMessages] = useState<string[]>([]);
  /** Tras un Fallback el servidor ha cerrado la conversación: hay que reenviar el contexto. */
  const [needsContext, setNeedsContext] = useState(false);
  const [lastSent, setLastSent] = useState<string | null>(null);

  const listRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLTextAreaElement>(null);

  useEffect(() => {
    getCategories()
      .then((categories) => {
        const names = categories.map((c) => c.level1?.name).filter((n): n is string => !!n);
        setShortcuts([...new Set(names)]);
      })
      .catch(() => setShortcuts([]));
  }, []);

  useEffect(() => {
    listRef.current?.scrollTo({ top: listRef.current.scrollHeight, behavior: 'smooth' });
  }, [items, busy]);

  const turns = items.filter((i): i is Extract<ChatItem, { kind: 'bot' }> => i.kind === 'bot');
  const lastBotId = turns.at(-1)?.id ?? null;
  const selectedTurn: Turn | null = turns.find((t) => t.id === selectedId)?.turn ?? turns.at(-1)?.turn ?? null;

  async function send(text: string, retry = false) {
    if (busy || closed) {
      return;
    }
    const wire = retry ? lastSent! : needsContext ? composeRoutingMessage(routingMessages, text) : text;

    if (!retry) {
      setItems((prev) => [...prev, { id: nextId++, kind: 'user', text }]);
      setInput('');
    }
    setBusy(true);
    setLastSent(wire);
    const started = performance.now();

    try {
      const response = await sendMessage(conversationId, wire);
      const reply = buildBotReply(response, redirects);
      const turn: Turn = {
        sent: wire,
        response,
        reply,
        redirectsBefore: redirects,
        elapsedMs: Math.round(performance.now() - started),
      };

      setConversationId(response.conversationId);
      // Lo ajeno al soporte de SM no se reenvía como contexto: solo confundiría al modelo.
      if (reply.fallbackAction !== 'redirect') {
        setRoutingMessages(needsContext || retry ? [wire] : [...routingMessages, wire]);
      }
      setNeedsContext(response.outcome === 'Fallback');
      if (reply.fallbackAction === 'reconduct') {
        setRedirects((r) => r + 1);
      }
      if (reply.closesConversation) {
        setClosed(true);
      }
      setItems((prev) => [...prev, { id: nextId++, kind: 'bot', turn }]);
      setSelectedId(null);
    } catch (error) {
      const text =
        error instanceof ApiError
          ? `Error ${error.status}: ${error.message}`
          : `No se ha podido contactar con la API (${(error as Error).message}). ¿Está arrancada en http://localhost:5022?`;
      setItems((prev) => [...prev, { id: nextId++, kind: 'error', text }]);
    } finally {
      setBusy(false);
      inputRef.current?.focus();
    }
  }

  async function newConversation() {
    if (conversationId) {
      await resetConversation(conversationId).catch(() => undefined);
    }
    setItems([]);
    setInput('');
    setSelectedId(null);
    setConversationId(null);
    setClosed(false);
    setRedirects(0);
    setRoutingMessages([]);
    setNeedsContext(false);
    setLastSent(null);
    inputRef.current?.focus();
  }

  function onSubmit(e: FormEvent) {
    e.preventDefault();
    if (input.trim()) {
      void send(input.trim());
    }
  }

  return (
    <div className="layout">
      <header>
        <h1>SM ChatRouter · Pruebas</h1>
        <button type="button" onClick={() => void newConversation()} disabled={busy}>
          Nueva conversación
        </button>
      </header>

      <main className="chat">
        <div className="messages" ref={listRef}>
          <div className="bubble bot welcome">
            <p>{WELCOME}</p>
          </div>
          {items.map((item) => {
            switch (item.kind) {
              case 'user':
                return (
                  <div key={item.id} className="bubble user">
                    <p>{item.text}</p>
                  </div>
                );
              case 'error':
                return (
                  <div key={item.id} className="bubble error">
                    <p>{item.text}</p>
                  </div>
                );
              case 'bot':
                return (
                  <BotMessage
                    key={item.id}
                    turn={item.turn}
                    selected={item.turn === selectedTurn}
                    isLast={item.id === lastBotId && !closed}
                    busy={busy}
                    shortcuts={shortcuts}
                    onSelect={() => setSelectedId(item.id)}
                    onShortcut={(s) => void send(s)}
                    onRetry={() => void send('', true)}
                  />
                );
            }
          })}
          {busy && (
            <div className="bubble bot typing" role="status" aria-label="Pensando…">
              <span />
              <span />
              <span />
            </div>
          )}
        </div>

        {closed ? (
          <div className="closed">
            Conversación terminada.{' '}
            <button type="button" className="primary" onClick={() => void newConversation()}>
              Nueva conversación
            </button>
          </div>
        ) : (
          <form className="composer" onSubmit={onSubmit}>
            <textarea
              ref={inputRef}
              value={input}
              rows={2}
              placeholder="Escribe tu consulta…"
              disabled={busy}
              autoFocus
              onChange={(e) => setInput(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === 'Enter' && !e.shiftKey) {
                  onSubmit(e);
                }
              }}
            />
            <button type="submit" className="primary" disabled={busy || !input.trim()}>
              Enviar
            </button>
          </form>
        )}
      </main>

      <aside className="panel">
        <h2>Diagnóstico del turno</h2>
        <Diagnostics turn={selectedTurn} maxRedirects={config.maxRedirects} />
      </aside>
    </div>
  );
}
