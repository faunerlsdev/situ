import path from 'node:path';
import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  output: 'standalone',
  outputFileTracingRoot: path.resolve(process.cwd(), '../..'),
  reactStrictMode: true,
  transpilePackages: ['@situ/shared'],
  agentRules: false,
  allowedDevOrigins: ['192.168.1.119'],
};

export default nextConfig;
