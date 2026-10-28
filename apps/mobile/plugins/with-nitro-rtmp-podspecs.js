const fs = require('fs');
const path = require('path');
const { withDangerousMod } = require('@expo/config-plugins');

const PACKAGE_NAME = 'react-native-nitro-rtmp-publisher';

/** Path of a vendored podspec, relative to the generated ios/ directory. */
function podspecRelativeToIos(projectRoot, fileName) {
  const packageJson = require.resolve(`${PACKAGE_NAME}/package.json`, { paths: [projectRoot] });
  const absolute = path.join(path.dirname(packageJson), 'podspecs', fileName);
  if (!fs.existsSync(absolute)) {
    throw new Error(`[situ] Falta el podspec ${absolute}`);
  }
  const relative = path.relative(path.join(projectRoot, 'ios'), absolute).split(path.sep).join('/');
  return relative.startsWith('.') ? relative : `./${relative}`;
}

function podspecPodfileLines(projectRoot) {
  return {
    HaishinKit: podspecRelativeToIos(projectRoot, 'HaishinKit.podspec'),
    RTMPHaishinKit: podspecRelativeToIos(projectRoot, 'RTMPHaishinKit.podspec'),
  };
}

/**
 * The publisher plugin always writes `../node_modules/<pkg>/podspecs/...`.
 * In this pnpm monorepo that directory is empty: the package is hoisted to the
 * repo root, and autolinking already points there with `../../../node_modules`.
 * CocoaPods expands the missing path and, because the file is absent, hands it
 * to OpenURI. Ruby 4 then rejects the filesystem path as a relative URI.
 */
function rewritePodfile(contents, projectRoot) {
  const lines = podspecPodfileLines(projectRoot);
  let rewritten = contents;
  for (const [name, podspec] of Object.entries(lines)) {
    const pattern = new RegExp(`pod '${name}',\\s*:podspec => '[^']*'`);
    if (!pattern.test(rewritten)) {
      throw new Error(
        `[situ] El Podfile no tiene \`pod '${name}'\`. El plugin de ${PACKAGE_NAME} tiene que correr antes que este.`,
      );
    }
    rewritten = rewritten.replace(pattern, `pod '${name}', :podspec => '${podspec}'`);
  }
  const hook = "system('node', File.expand_path('../plugins/patch-rtmp-socket-logs.js', __dir__))";
  if (rewritten.includes('post_install do |installer|') && !rewritten.includes('patch-rtmp-socket-logs.js')) {
    rewritten = rewritten.replace(
      'post_install do |installer|\n',
      `post_install do |installer|\n    ${hook}\n`,
    );
  }
  return rewritten;
}

function withNitroRtmpPodspecs(config) {
  return withDangerousMod(config, [
    'ios',
    async (config) => {
      const projectRoot = config.modRequest.projectRoot;
      const podfilePath = path.join(config.modRequest.platformProjectRoot, 'Podfile');
      const before = fs.readFileSync(podfilePath, 'utf8');
      const next = rewritePodfile(before, projectRoot);
      if (next !== before) {
        fs.writeFileSync(podfilePath, next);
      }
      return config;
    },
  ]);
}

module.exports = withNitroRtmpPodspecs;
module.exports.podspecPodfileLines = podspecPodfileLines;
module.exports.rewritePodfile = rewritePodfile;
