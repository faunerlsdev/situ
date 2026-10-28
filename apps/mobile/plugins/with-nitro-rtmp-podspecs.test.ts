import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';

const mobileRoot = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const require = createRequire(import.meta.url);
const { podspecPodfileLines, rewritePodfile } = require('./with-nitro-rtmp-podspecs.js') as {
  podspecPodfileLines: (projectRoot: string) => { HaishinKit: string; RTMPHaishinKit: string };
  rewritePodfile: (contents: string, projectRoot: string) => string;
};

const injected = `
target 'SITU' do
  pod 'HaishinKit',     :podspec => '../node_modules/react-native-nitro-rtmp-publisher/podspecs/HaishinKit.podspec'
  pod 'RTMPHaishinKit', :podspec => '../node_modules/react-native-nitro-rtmp-publisher/podspecs/RTMPHaishinKit.podspec'
end
`;

describe('nitro rtmp podspecs', () => {
  it('points the vendored podspecs at the hoisted package', () => {
    const lines = podspecPodfileLines(mobileRoot);
    for (const podspec of Object.values(lines)) {
      const absolute = path.resolve(mobileRoot, 'ios', podspec);
      expect(fs.existsSync(absolute)).toBe(true);
      expect(podspec.includes('/apps/mobile/node_modules/')).toBe(false);
    }
  });

  it('replaces the publisher plugin paths in a Podfile', () => {
    const lines = podspecPodfileLines(mobileRoot);
    const rewritten = rewritePodfile(injected, mobileRoot);
    expect(rewritten).toContain(`:podspec => '${lines.HaishinKit}'`);
    expect(rewritten).toContain(`:podspec => '${lines.RTMPHaishinKit}'`);
    expect(rewritten).not.toContain(":podspec => '../node_modules/");
  });

  it('keeps the RTMPS debug hook inside post_install', () => {
    const podfile = `${injected}\n  post_install do |installer|\n    react_native_post_install()\n  end\n`;
    const rewritten = rewritePodfile(podfile, mobileRoot);
    expect(rewritten).toContain('patch-rtmp-socket-logs.js');
  });
});
