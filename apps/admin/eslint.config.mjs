import base from '@situ/config/eslint';

const config = [
  ...base,
  {
    files: ['src/lib/upstream.ts', 'src/lib/client.ts', 'src/app/api/**/*.ts'],
    rules: {
      'no-restricted-globals': [
        'error',
        { name: 'alert', message: 'Usa notify o alertModal.' },
        { name: 'confirm', message: 'Importa confirm desde @situ/shared.' },
      ],
    },
  },
  {
    files: ['src/app/login/page.tsx', 'src/app/(panel)/avatares/page.tsx', 'src/app/(panel)/categorias/page.tsx'],
    rules: {
      '@next/next/no-img-element': 'off',
    },
  },
];

export default config;
