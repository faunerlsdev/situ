/** Copies store env names into the EXPO_PUBLIC_ variables Metro and app.config both read. */

function applyStoreEnv() {
  const raw = (process.env.EXPO_PUBLIC_DOMAIN_ROOT || process.env.DOMAIN_ROOT || '').trim();
  const domain = raw.replace(/^https?:\/\//, '').replace(/\/$/, '');
  if (domain) {
    process.env.EXPO_PUBLIC_DOMAIN_ROOT = domain;
  }

  if (!process.env.EXPO_PUBLIC_API_URL?.trim()) {
    const explicit = process.env.API_BASE_URL?.trim();
    if (explicit) {
      process.env.EXPO_PUBLIC_API_URL = explicit;
    } else if (domain) {
      process.env.EXPO_PUBLIC_API_URL = `https://api.${domain}`;
    }
  }

  return domain;
}

module.exports = { applyStoreEnv };
