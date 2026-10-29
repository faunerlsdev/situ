import path from 'node:path';
import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  output: 'standalone',
  outputFileTracingRoot: path.resolve(process.cwd(), '../..'),
  reactStrictMode: true,
  transpilePackages: ['@situ/shared'],
  agentRules: false,
  async redirects() {
    return [
      { source: '/politica-de-tratamiento-de-datos', destination: '/privacidad', statusCode: 301 },
      { source: '/tratamiento-datos', destination: '/privacidad', statusCode: 301 },
      { source: '/contenido-y-moderacion', destination: '/terminos#14', statusCode: 301 },
    ];
  },
  outputFileTracingIncludes: {
    '/privacidad': ['../../docs/legal/politica-privacidad-situ-v1.1.md'],
    '/terminos': ['../../docs/legal/terminos-situ-v1.0.md'],
  },
};

export default nextConfig;
