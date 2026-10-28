import type { RoutingOutcome } from '../api';

const LABELS: Record<RoutingOutcome, string> = {
  NeedsMoreInformation: 'Pregunta',
  Routed: 'Routed',
  Fallback: 'Fallback',
};

export function OutcomeBadge({ outcome }: { outcome: RoutingOutcome }) {
  return (
    <span className={`badge badge-${outcome}`} title={outcome}>
      {LABELS[outcome]}
    </span>
  );
}
