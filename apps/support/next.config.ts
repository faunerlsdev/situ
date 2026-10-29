import path from 'node:path';
import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  output: 'standalone',
  outputFileTracingRoot: path.resolve(process.cwd(), '../..'),
  reactStrictMode: true,
  transpilePackages: ['@situ/shared'],
  agentRules: false,
  async redirects() {
    return [{ source: '/solicitudes', destination: '/retiro-contenido', statusCode: 301 }];
  },
};

export default nextConfig;
