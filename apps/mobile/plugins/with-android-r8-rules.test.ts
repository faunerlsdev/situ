import { createRequire } from 'node:module';
import { describe, expect, it } from 'vitest';

const require = createRequire(import.meta.url);
const { appendSlf4jRule, appendWorkManagerRules, RULE, WORK_MANAGER_RULES } = require(
  './with-android-r8-rules.js',
) as {
  appendSlf4jRule: (contents: string) => string;
  appendWorkManagerRules: (contents: string) => string;
  RULE: string;
  WORK_MANAGER_RULES: string;
};

describe('android R8 rules', () => {
  it('adds the SLF4J dontwarn rule once', () => {
    const first = appendSlf4jRule('# existing\n');
    expect(first).toContain(RULE);
    expect(first.startsWith('# existing')).toBe(true);
    expect(appendSlf4jRule(first)).toBe(first);
  });

  it('keeps the WorkManager constructors R8 would remove', () => {
    const first = appendWorkManagerRules(appendSlf4jRule(''));
    expect(first).toContain(RULE);
    expect(first).toContain(WORK_MANAGER_RULES);
    expect(appendWorkManagerRules(first)).toBe(first);
  });
});
