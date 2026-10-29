import type { Category, CategorySummary, ChatResponse } from './api';
import { decideFallback, type FallbackAction } from './fallbackPolicy';
import { shortcutsFor, type CategoryLocation, type Shortcut } from './shortcuts';

export const config = {
  categoryBaseUrl: (import.meta.env.VITE_CATEGORY_BASE_URL ?? '').trim().replace(/\/+$/, ''),
  maxRedirects: Number(import.meta.env.VITE_MAX_REDIRECTS ?? 2) || 0,
};

/** Reconducción por defecto ante un NoMatch si el modelo no ha redactado la suya. */
const REDIRECT_TEXT =
  'Estoy aquí para ayudarte con tus consultas sobre SM: pedidos, licencias digitales, tu cuenta o ' +
  'materiales docentes. ¿En qué puedo ayudarte?';

export interface FormLink {
  href: string | null;
  /** Lo que se muestra: la ruta N1 > N2 > N3. */
  label: string;
  categoryId: string;
}

export interface BotReply {
  text: string;
  link: FormLink | null;
  /** Atajos de categorías (N1, N2 o N3 según la ubicación) que equivalen a escribir su texto. */
  shortcuts: Shortcut[];
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

/** Árbol de categorías y ubicación del usuario en él, para elegir los atajos. */
export interface Navigation {
  categories: readonly Category[];
  location: CategoryLocation | null;
}

/** Traduce la respuesta del router al mensaje que ve el usuario. */
export function buildBotReply(
  response: ChatResponse,
  redirects: number,
  navigation: Navigation,
  maxRedirects = config.maxRedirects,
): BotReply {
  const reply = buildReply(response, redirects, maxRedirects);
  return {
    ...reply,
    shortcuts: shortcutsFor(response, reply.fallbackAction, navigation.location, navigation.categories),
  };
}

function buildReply(response: ChatResponse, redirects: number, maxRedirects: number): BotReply {
  const base: BotReply = {
    text: '',
    link: null,
    shortcuts: [],
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
            fallbackAction: action,
          };
        case 'redirect':
          return {
            ...base,
            text: response.question?.trim() || REDIRECT_TEXT,
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
