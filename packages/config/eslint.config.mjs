import nextCoreWebVitals from 'eslint-config-next/core-web-vitals';
import nextTypescript from 'eslint-config-next/typescript';

const core = nextCoreWebVitals.default ?? nextCoreWebVitals;
const typescript = nextTypescript.default ?? nextTypescript;

/** Flat config for the Next.js apps. Apps re-export this file. */
export default [
  ...core,
  ...typescript,
  {
    ignores: ['.next/**', 'out/**', 'next-env.d.ts'],
  },
  {
    rules: {
      'no-alert': 'error',
      'no-restricted-globals': [
        'error',
        { name: 'fetch', message: 'Usa el cliente HTTP de @situ/shared (core/http).' },
        { name: 'alert', message: 'Usa notify o alertModal.' },
        { name: 'confirm', message: 'Importa confirm desde @situ/shared.' },
      ],
      'no-restricted-imports': [
        'error',
        {
          paths: [
            {
              name: 'axios',
              message: 'Usa el cliente HTTP de @situ/shared (core/http).',
            },
          ],
        },
      ],
    },
  },
];
