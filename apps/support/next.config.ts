import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  reactStrictMode: true,
  transpilePackages: ['@situ/shared'],
  agentRules: false,
};

export default nextConfig;
