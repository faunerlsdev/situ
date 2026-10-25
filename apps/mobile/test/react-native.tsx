import { createElement } from 'react';
import type { ReactNode } from 'react';

type Props = { children?: ReactNode; [key: string]: unknown };

function host(name: string) {
  return function Host(props: Props) {
    return createElement(name, props, props.children);
  };
}

export const View = host('View');
export const Text = host('Text');
export const Pressable = host('Pressable');
export const TextInput = host('TextInput');
export const Switch = host('Switch');
export const Image = host('Image');
export const ScrollView = host('ScrollView');
export const RefreshControl = host('RefreshControl');

export function Modal(props: Props & { visible?: boolean }) {
  if (!props.visible) {
    return null;
  }
  return createElement('Modal', props, props.children);
}

export const StyleSheet = {
  create<T>(styles: T): T {
    return styles;
  },
  absoluteFill: {},
};

export const Platform = {
  OS: 'ios',
  select<T>(options: { ios?: T; android?: T; default?: T }): T | undefined {
    return options.ios ?? options.default;
  },
};

export const AccessibilityInfo = {
  isReduceMotionEnabled: () => Promise.resolve(false),
  addEventListener: () => ({ remove: () => undefined }),
};
