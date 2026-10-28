import type { Turn } from '../types';
import { OutcomeBadge } from './OutcomeBadge';

const ACTION_LABELS = {
  reconduct: 'Reconducir (pedir más detalle)',
  retry: 'Reintentar (error técnico)',
  giveUp: 'Rendirse (formulario por defecto)',
} as const;

export function Diagnostics({ turn, maxRedirects }: { turn: Turn | null; maxRedirects: number }) {
  if (!turn) {
    return <p className="muted">Envía un mensaje para ver aquí la decisión del router en cada turno.</p>;
  }

  const { response, reply } = turn;
  const parameters = Object.entries(response.parameters);

  return (
    <div className="diagnostics">
      <dl>
        <dt>Outcome</dt>
        <dd>
          <OutcomeBadge outcome={response.outcome} />
        </dd>

        <dt>Categoría</dt>
        <dd>
          {response.category ? (
            <>
              {response.category.path}
              <br />
              <code>{response.category.id}</code>
            </>
          ) : (
            <span className="muted">—</span>
          )}
        </dd>

        {response.question && (
          <>
            <dt>Pregunta</dt>
            <dd>{response.question}</dd>
          </>
        )}

        {response.outcome === 'Fallback' && (
          <>
            <dt>Motivo</dt>
            <dd>
              <code>{response.fallbackReason ?? '—'}</code>
            </dd>
            <dt>Acción del chat</dt>
            <dd>{reply.fallbackAction ? ACTION_LABELS[reply.fallbackAction] : '—'}</dd>
          </>
        )}

        <dt>Reconducciones</dt>
        <dd>
          {turn.redirectsBefore} / {maxRedirects}
        </dd>

        <dt>Faltan</dt>
        <dd>
          {response.missingParameters.length ? (
            <ul className="chips">
              {response.missingParameters.map((p) => (
                <li key={p}>{p}</li>
              ))}
            </ul>
          ) : (
            <span className="muted">—</span>
          )}
        </dd>

        <dt>Conversación</dt>
        <dd>
          <code>{response.conversationId}</code>
        </dd>

        <dt>Tiempo</dt>
        <dd>{turn.elapsedMs} ms</dd>
      </dl>

      <h3>Parámetros recogidos</h3>
      {parameters.length ? (
        <table>
          <tbody>
            {parameters.map(([k, v]) => (
              <tr key={k}>
                <th>{k}</th>
                <td>{v}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <p className="muted">Ninguno.</p>
      )}

      <h3>Texto enviado a la API</h3>
      <pre className="sent">{turn.sent}</pre>

      <details>
        <summary>JSON de la respuesta</summary>
        <pre>{JSON.stringify(response, null, 2)}</pre>
      </details>
    </div>
  );
}
