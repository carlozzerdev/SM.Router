import type { CategorySummary, ChatResponse } from './api';
import { decideFallback, type FallbackAction } from './fallbackPolicy';

export const config = {
  categoryBaseUrl: (import.meta.env.VITE_CATEGORY_BASE_URL ?? '').trim().replace(/\/+$/, ''),
  maxRedirects: Number(import.meta.env.VITE_MAX_REDIRECTS ?? 2) || 0,
};

export interface FormLink {
  href: string | null;
  /** Lo que se muestra: la ruta N1 > N2 > N3. */
  label: string;
  categoryId: string;
}

export interface BotReply {
  text: string;
  link: FormLink | null;
  /** Atajos de categorías de primer nivel para reconducir. */
  showShortcuts: boolean;
  showRetry: boolean;
  /** Routed o rendición: la conversación termina y hay que empezar otra. */
  closesConversation: boolean;
  /** Acción tomada ante un Fallback (solo para diagnóstico). */
  fallbackAction: FallbackAction | null;
}

export function formLink(category: CategorySummary, baseUrl = config.categoryBaseUrl): FormLink {
  return {
    href: baseUrl ? `${baseUrl}/${category.id}` : null,
    label: category.path || category.name,
    categoryId: category.id,
  };
}

/** Traduce la respuesta del router al mensaje que ve el usuario. */
export function buildBotReply(response: ChatResponse, redirects: number, maxRedirects = config.maxRedirects): BotReply {
  const base: BotReply = {
    text: '',
    link: null,
    showShortcuts: false,
    showRetry: false,
    closesConversation: false,
    fallbackAction: null,
  };

  switch (response.outcome) {
    case 'NeedsMoreInformation':
      return { ...base, text: response.question ?? '¿Podrías darme más detalles sobre lo que necesitas?' };

    case 'Routed':
      return {
        ...base,
        text: 'Este es el formulario para tu solicitud:',
        link: response.category ? formLink(response.category) : null,
        closesConversation: true,
      };

    case 'Fallback': {
      const action = decideFallback(response.fallbackReason, redirects, maxRedirects);
      const fallbackLink = response.category ? formLink(response.category) : null;
      switch (action) {
        case 'reconduct':
          return {
            ...base,
            text:
              'No he terminado de entender tu consulta. ¿Puedes contarme un poco más? Por ejemplo, si eres ' +
              'profesor, alumno, familia o librería, y si es sobre un pedido, una licencia digital, tu cuenta… ' +
              'También puedes elegir uno de estos temas:',
            showShortcuts: true,
            fallbackAction: action,
          };
        case 'retry':
          return {
            ...base,
            text:
              'Ha habido un problema técnico al procesar tu mensaje. Puedes reintentarlo o, si lo prefieres, ' +
              'usar el formulario de consulta general:',
            link: fallbackLink,
            showRetry: true,
            fallbackAction: action,
          };
        case 'giveUp':
          return {
            ...base,
            text: 'No he podido identificar el formulario exacto. Puedes enviarnos tu consulta desde aquí:',
            link: fallbackLink,
            closesConversation: true,
            fallbackAction: action,
          };
      }
    }
  }
}
