import type { Category, CategoryNode, ChatResponse } from './api';
import type { FallbackAction } from './fallbackPolicy';

/**
 * Dónde está el usuario en el árbol de categorías según el último atajo que ha pulsado.
 * El servidor no devuelve una ubicación parcial, así que se sigue en el cliente.
 */
export interface CategoryLocation {
  level1: CategoryNode;
  level2?: CategoryNode;
}

export interface Shortcut {
  /** Lo que se ve en el botón y en la burbuja del usuario. */
  label: string;
  /** Lo que se envía a la API: la ruta, porque los nombres de N2 y N3 se repiten entre ramas. */
  wire: string;
  /** Ubicación en la que queda el usuario al pulsarlo. */
  location: CategoryLocation;
}

/** Nodos distintos por slug, en el orden en que aparecen (las categorías se repiten por idioma). */
function distinct<T>(items: T[], key: (item: T) => string): T[] {
  const seen = new Set<string>();
  return items.filter((item) => {
    const k = key(item);
    if (seen.has(k)) {
      return false;
    }
    seen.add(k);
    return true;
  });
}

export function level1Shortcuts(categories: readonly Category[]): Shortcut[] {
  const nodes = categories.map((c) => c.level1).filter((n): n is CategoryNode => !!n);
  return distinct(nodes, (n) => n.slug).map((level1) => ({
    label: level1.name,
    wire: level1.name,
    location: { level1 },
  }));
}

export function level2Shortcuts(categories: readonly Category[], level1Slug: string): Shortcut[] {
  const inBranch = categories.filter((c) => c.level1?.slug === level1Slug && c.level2);
  return distinct(inBranch, (c) => c.level2!.slug).map((c) => ({
    label: c.level2!.name,
    wire: `${c.level1!.name} > ${c.level2!.name}`,
    location: { level1: c.level1!, level2: c.level2! },
  }));
}

export function level3Shortcuts(categories: readonly Category[], level1Slug: string, level2Slug: string): Shortcut[] {
  const inBranch = categories.filter((c) => c.level1?.slug === level1Slug && c.level2?.slug === level2Slug);
  return distinct(inBranch, (c) => c.name).map((c) => ({
    label: c.name,
    wire: `${c.level1!.name} > ${c.level2!.name} > ${c.name}`,
    location: { level1: c.level1!, level2: c.level2! },
  }));
}

/** Atajos que acompañan a una respuesta del router. */
export function shortcutsFor(
  response: ChatResponse,
  action: FallbackAction | null,
  location: CategoryLocation | null,
  categories: readonly Category[],
): Shortcut[] {
  if (response.outcome === 'Fallback') {
    return action === 'reconduct' || action === 'redirect' ? level1Shortcuts(categories) : [];
  }
  // Con una N3 candidata solo faltan parámetros: el usuario tiene que escribirlos.
  if (response.outcome !== 'NeedsMoreInformation' || response.category || !location) {
    return [];
  }
  return location.level2
    ? level3Shortcuts(categories, location.level1.slug, location.level2.slug)
    : level2Shortcuts(categories, location.level1.slug);
}
