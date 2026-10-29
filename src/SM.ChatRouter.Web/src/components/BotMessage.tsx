import type { Shortcut } from '../shortcuts';
import type { Turn } from '../types';
import { OutcomeBadge } from './OutcomeBadge';
import { Shortcuts } from './Shortcuts';

interface Props {
  turn: Turn;
  selected: boolean;
  isLast: boolean;
  busy: boolean;
  onSelect: () => void;
  onShortcut: (shortcut: Shortcut) => void;
  onRetry: () => void;
}

export function BotMessage({ turn, selected, isLast, busy, onSelect, onShortcut, onRetry }: Props) {
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

      <Shortcuts shortcuts={reply.shortcuts} disabled={!actionsEnabled} onShortcut={onShortcut} />

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

      {response.explanation && (
        <p className="explanation" title="Explicación del modelo">
          {response.explanation}
        </p>
      )}
    </div>
  );
}
