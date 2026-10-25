import type { InboxNotification } from '@situ/shared';
import { notificationMessages } from '@situ/shared';
import type { ReactElement } from 'react';
import { create, act } from 'react-test-renderer';
import type { ReactTestRenderer } from 'react-test-renderer';
import { Switch } from 'react-native';
import { describe, expect, it } from 'vitest';

import { NotificationRow } from './notification-row';
import { SettingsToggle } from './settings-toggle';

function render(node: ReactElement) {
  let tree: ReactTestRenderer | undefined;
  act(() => {
    tree = create(node);
  });
  if (!tree) {
    throw new Error('render failed');
  }
  return tree;
}

const item: InboxNotification = {
  id: 'n1',
  type: 'NearbyReport',
  title: 'En tu ciudad',
  body: 'Nuevo reporte en Bogotá: Hurto',
  targetType: 'Report',
  targetId: 'r1',
  reportId: 'r1',
  createdAt: new Date().toISOString(),
  isRead: false,
  available: false,
};

describe('notification row', () => {
  it('dims an unavailable alert and keeps it from opening a report', () => {
    let opened = 0;
    const tree = render(<NotificationRow item={item} onPress={() => { opened += 1; }} />);
    const json = JSON.stringify(tree.toJSON());
    expect(json).toContain(notificationMessages.unavailable);
    const row = tree.root.findByProps({ accessibilityRole: 'button' });
    expect(row.props.style).toEqual(expect.arrayContaining([expect.objectContaining({ opacity: 0.45 })]));
    act(() => {
      row.props.onPress();
    });
    expect(opened).toBe(1);
  });
});

describe('settings toggle', () => {
  it('shows the next value and reverts when the save fails', async () => {
    const tree = render(<SettingsToggle label="Cerca" value save={async () => false} />);
    const control = tree.root.findByType(Switch);
    expect(control.props.value).toBe(true);
    await act(async () => {
      control.props.onValueChange(false);
      await Promise.resolve();
    });
    expect(tree.root.findByType(Switch).props.value).toBe(true);
  });

  it('keeps the optimistic value when the save succeeds', async () => {
    const tree = render(<SettingsToggle label="Cerca" value save={async () => true} />);
    const control = tree.root.findByType(Switch);
    await act(async () => {
      control.props.onValueChange(false);
      await Promise.resolve();
    });
    expect(tree.root.findByType(Switch).props.value).toBe(false);
  });
});
