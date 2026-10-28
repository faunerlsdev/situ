import path from 'node:path';
import { defineConfig } from 'vitest/config';

export default defineConfig({
  esbuild: { jsx: 'automatic' },
  resolve: {
    alias: {
      'react-native': path.resolve(__dirname, 'test/react-native.tsx'),
      '@': path.resolve(__dirname),
    },
  },
  test: {
    environment: 'node',
    include: ['features/**/*.test.ts', 'features/**/*.test.tsx', 'plugins/**/*.test.ts'],
  },
});
