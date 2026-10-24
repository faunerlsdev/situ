import type { NextConfig } from 'next';

const nextConfig: NextConfig = {
  reactStrictMode: true,
  transpilePackages: ['@situ/shared'],
  agentRules: false,
  allowedDevOrigins: ['192.168.1.119'],
};

export default nextConfig;
