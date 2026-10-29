import type { Turn } from '../types';
import { OutcomeBadge } from './OutcomeBadge';

interface Props {
  turn: Turn;
  selected: boolean;
  isLast: boolean;
  busy: boolean;
  shortcuts: string[];
  onSelect: () => void;
  onShortcut: (text: string) => void;
  onRetry: () => void;
}

export function BotMessage({ turn, selected, isLast, busy, shortcuts, onSelect, onShortcut, onRetry }: Props) {
  const { reply, response } = turn;
  // Los botones solo tienen sentido en el último mensaje del bot.
  const actionsEnabled = isLast && !busy;

  return (
    <div className={`bubble bot${selected ? ' selected' : ''}`} onClick={onSelect}>
      <div className="bubble-meta">
        <OutcomeBadge outcome={response.outcome} />
        {response.fallbackReason && <span className="reason">{response.fallbackReason}</span>}
        <span className="elapsed">{(turn.elapsedMs / 1000).toFixed(1)} s</span>
      </div>
      <p>{reply.text}</p>

      {response.explanation && (
        <p className="explanation" title="Explicación del modelo">
          {response.explanation}
        </p>
      )}

      {reply.link && (
        <div className="form-link">
          {reply.link.href ? (
            <a href={reply.link.href} target="_blank" rel="noreferrer" onClick={(e) => e.stopPropagation()}>
              {reply.link.label}
            </a>
          ) : (
            <span>{reply.link.label}</span>
          )}
          <code>{reply.link.categoryId}</code>
        </div>
      )}

      {reply.showShortcuts && shortcuts.length > 0 && (
        <div className="shortcuts">
          {shortcuts.map((s) => (
            <button
              key={s}
              type="button"
              disabled={!actionsEnabled}
              onClick={(e) => {
                e.stopPropagation();
                onShortcut(s);
              }}
            >
              {s}
            </button>
          ))}
        </div>
      )}

      {reply.showRetry && (
        <div className="shortcuts">
          <button
            type="button"
            className="primary"
            disabled={!actionsEnabled}
            onClick={(e) => {
              e.stopPropagation();
              onRetry();
            }}
          >
            Reintentar
          </button>
        </div>
      )}
    </div>
  );
}
