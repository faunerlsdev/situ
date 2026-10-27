const fs = require('fs');
const path = require('path');
const { getDefaultConfig } = require('expo/metro-config');
const { applyStoreEnv } = require('./public-env');

loadRootEnv(path.resolve(__dirname, '../../.env'));
applyStoreEnv();

// SDK 58 already watches the pnpm workspace. extraNodeModules points Metro at the
// single hoisted react and react-native (overrides in pnpm-workspace.yaml).
// disableHierarchicalLookup stays at Expo's default: expo-doctor fails if it is true.
const config = getDefaultConfig(__dirname);
const monorepoRoot = path.resolve(__dirname, '../..');

config.watchFolders = Array.from(new Set([...(config.watchFolders ?? []), monorepoRoot]));
config.resolver.nodeModulesPaths = Array.from(
  new Set([
    ...(config.resolver.nodeModulesPaths ?? []),
    path.resolve(__dirname, 'node_modules'),
    path.resolve(monorepoRoot, 'node_modules'),
  ]),
);
config.resolver.extraNodeModules = {
  ...config.resolver.extraNodeModules,
  react: path.resolve(monorepoRoot, 'node_modules/react'),
  'react-native': path.resolve(monorepoRoot, 'node_modules/react-native'),
};

function loadRootEnv(file) {
  if (!fs.existsSync(file)) {
    return;
  }

  for (const line of fs.readFileSync(file, 'utf8').split('\n')) {
    const trimmed = line.trim();
    if (!trimmed || trimmed.startsWith('#')) {
      continue;
    }

    const separator = trimmed.indexOf('=');
    if (separator <= 0) {
      continue;
    }

    const key = trimmed.slice(0, separator).trim();
    if (!key.startsWith('EXPO_PUBLIC_') && !key.startsWith('NEXT_PUBLIC_')) {
      continue;
    }

    let value = trimmed.slice(separator + 1).trim();
    if (
      (value.startsWith('"') && value.endsWith('"')) ||
      (value.startsWith("'") && value.endsWith("'"))
    ) {
      value = value.slice(1, -1);
    }

    process.env[key] = value;
  }
}

module.exports = config;
