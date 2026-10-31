import path from 'node:path';
import { defineConfig } from 'vitest/config';

function pngMock() {
  return {
    name: 'png-mock',
    enforce: 'pre' as const,
    transform(code: string, id: string) {
      if (id.includes('node_modules') || !code.includes('.png')) {
        return null;
      }
      const next = code.replace(/require\(\s*(['"])[^'"]+\.png\1\s*\)/g, '1');
      return next === code ? null : { code: next, map: null };
    },
  };
}

export default defineConfig({
  esbuild: { jsx: 'automatic' },
  plugins: [pngMock()],
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
