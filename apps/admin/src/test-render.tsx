import { screen } from '@testing-library/dom';
import { act, type ReactNode } from 'react';
import { createRoot } from 'react-dom/client';

export function render(node: ReactNode) {
  const container = document.createElement('div');
  document.body.appendChild(container);
  const root = createRoot(container);
  act(() => {
    root.render(node);
  });
  return {
    container,
    unmount() {
      act(() => root.unmount());
      container.remove();
    },
  };
}

export { screen };
