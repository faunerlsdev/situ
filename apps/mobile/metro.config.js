const { getDefaultConfig } = require('expo/metro-config');

// SDK 58 detects the pnpm workspace when Metro starts from expo/metro-config.
const config = getDefaultConfig(__dirname);

module.exports = config;
