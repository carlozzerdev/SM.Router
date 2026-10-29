import type { Shortcut } from '../shortcuts';

interface Props {
  shortcuts: Shortcut[];
  disabled: boolean;
  onShortcut: (shortcut: Shortcut) => void;
}

/** Botones de categorías: pulsar uno equivale a escribir su texto. */
export function Shortcuts({ shortcuts, disabled, onShortcut }: Props) {
  if (shortcuts.length === 0) {
    return null;
  }
  return (
    <div className="shortcuts">
      {shortcuts.map((s) => (
        <button
          key={s.wire}
          type="button"
          title={s.wire}
          disabled={disabled}
          onClick={(e) => {
            e.stopPropagation();
            onShortcut(s);
          }}
        >
          {s.label}
        </button>
      ))}
    </div>
  );
}
