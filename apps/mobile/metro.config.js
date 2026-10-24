const fs = require('fs');
const path = require('path');
const { getDefaultConfig } = require('expo/metro-config');

loadRootEnv(path.resolve(__dirname, '../../.env'));

// SDK 58 detects the pnpm workspace when Metro starts from expo/metro-config.
const config = getDefaultConfig(__dirname);

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
