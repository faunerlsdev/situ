import type { ReactElement } from 'react';
import { create, act } from 'react-test-renderer';
import type { ReactTestRenderer } from 'react-test-renderer';
import { Switch } from 'react-native';
import { describe, expect, it } from 'vitest';

import { CommentComposer } from './comment-composer';
import { ComplaintSheet } from './complaint-sheet';

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

describe('comment composer', () => {
  it('explains anonymity and locks the switch for a public minor', () => {
    const tree = render(
      <CommentComposer
        text=""
        onChangeText={() => undefined}
        anonymous
        anonymousLocked
        onAnonymousChange={() => undefined}
        disabled={false}
        pending={false}
        notice={null}
        inlineError={null}
        countdown={null}
        replyingTo={null}
        onCancelReply={() => undefined}
        onSend={() => undefined}
      />,
    );
    const json = JSON.stringify(tree.toJSON());
    expect(json).toContain('En un reporte público, si eres menor, el comentario queda anónimo.');
    const switches = tree.root.findAllByType(Switch);
    expect(switches[0]?.props.disabled).toBe(true);
    expect(switches[0]?.props.value).toBe(true);
  });

  it('shows the daily limit next to the field', () => {
    const tree = render(
      <CommentComposer
        text="hola"
        onChangeText={() => undefined}
        anonymous
        anonymousLocked={false}
        onAnonymousChange={() => undefined}
        disabled={false}
        pending={false}
        notice={null}
        inlineError="Llegaste al límite de comentarios anónimos de hoy."
        countdown={null}
        replyingTo={null}
        onCancelReply={() => undefined}
        onSend={() => undefined}
      />,
    );
    expect(JSON.stringify(tree.toJSON())).toContain('Llegaste al límite de comentarios anónimos de hoy.');
  });
});

describe('complaint sheet', () => {
  it('lists the reasons and keeps a duplicate from being sent again', () => {
    let sent = 0;
    const tree = render(
      <ComplaintSheet
        visible
        pending={false}
        duplicateMessage="Ya enviaste una denuncia sobre esto. La estamos revisando."
        animationType="none"
        onClose={() => undefined}
        onSubmit={() => { sent += 1; }}
      />,
    );
    const json = JSON.stringify(tree.toJSON());
    expect(json).toContain('Información falsa');
    expect(json).toContain('Seguridad de menores');
    expect(json).toContain('Ya enviaste una denuncia');
    const send = tree.root.findAll((node) => node.props.accessibilityLabel === 'Enviar denuncia');
    expect(send[0]?.props.disabled).toBe(true);
    act(() => {
      send[0]?.props.onPress();
    });
    expect(sent).toBe(0);
  });
});
