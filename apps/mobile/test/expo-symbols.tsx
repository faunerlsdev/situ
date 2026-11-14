import { createElement } from 'react';
import type { ReactNode } from 'react';

export function SymbolView({ children }: { children?: ReactNode }) {
  return createElement('SymbolView', null, children);
}
