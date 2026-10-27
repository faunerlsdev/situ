'use client';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useState } from 'react';
import { api, readBrowserSession } from '@/lib/http';
import { deletionView } from './deletion-copy';
import { DeleteAccountView } from './delete-account-view';

const fallbackSupportUrl = 'https://ayuda.somossitu.com/contacto';
const fallbackSupportEmail = 'soporte@somossitu.com';
const legacySupportUrl = 'https://somossitu.com/soporte';

export function DeleteAccountPanel() {
  const queryClient = useQueryClient();
  const [revision, setRevision] = useState(0);
  const session = useQuery({ queryKey: ['session'], queryFn: readBrowserSession });
  const signedIn = session.data?.signedIn === true;
  const profile = useQuery({
    queryKey: ['me', 'deletion'],
    enabled: signedIn,
    queryFn: () => api.sessionProfile(),
  });
  const config = useQuery({ queryKey: ['app-config'], queryFn: async () => (await api.appConfig()).data });
  const configuredSupportUrl = config.data?.legal.supportUrl;
const supportUrl = !configuredSupportUrl || configuredSupportUrl === legacySupportUrl ? fallbackSupportUrl : configuredSupportUrl;
const state = deletionView(signedIn, profile.data ?? null);

  return (
    <DeleteAccountView
      key={revision}
      state={state}
      supportUrl={supportUrl}
      supportEmail={fallbackSupportEmail}
      onRequest={async (password) => {
        await api.requestAccountDeletion(password);
        await queryClient.invalidateQueries({ queryKey: ['me', 'deletion'] });
        setRevision((value) => value + 1);
      }}
      onCancel={async () => {
        await api.cancelAccountDeletion();
        await queryClient.invalidateQueries({ queryKey: ['me', 'deletion'] });
        setRevision((value) => value + 1);
      }}
    />
  );
}
