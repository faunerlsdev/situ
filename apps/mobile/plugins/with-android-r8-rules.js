const fs = require('fs');
const path = require('path');
const { withDangerousMod } = require('@expo/config-plugins');

const RULE = '-dontwarn org.slf4j.impl.StaticLoggerBinder';

const WORK_MANAGER_RULES = `-keep class * extends androidx.room.RoomDatabase { <init>(); }
-keep class * extends androidx.work.InputMerger { <init>(); }`;

/**
 * RootEncoder pulls SLF4J 1.x. LoggerFactory.bind() names StaticLoggerBinder,
 * and the binding jar is optional: at runtime SLF4J continues without it.
 * R8 treats that missing class as a release error unless this rule is present.
 */
function appendSlf4jRule(contents) {
  if (contents.includes(RULE)) {
    return contents;
  }
  const body = contents.trimEnd();
  const prefix = body.length > 0 ? `${body}\n\n` : '';
  return `${prefix}# SLF4J 1.x looks up this binding at runtime and falls back if it is absent.\n${RULE}\n`;
}

function appendWorkManagerRules(contents) {
  if (contents.includes('extends androidx.room.RoomDatabase { <init>(); }')) {
    return contents;
  }
  const body = contents.trimEnd();
  const prefix = body.length > 0 ? `${body}\n\n` : '';
  return `${prefix}# react-native-maps starts WorkManager 2.9.1. R8 full mode removes the\n# no-arg constructors Room and InputMerger instantiate by reflection.\n${WORK_MANAGER_RULES}\n`;
}

function withAndroidR8Rules(config) {
  return withDangerousMod(config, [
    'android',
    async (config) => {
      const file = path.join(config.modRequest.platformProjectRoot, 'app', 'proguard-rules.pro');
      const current = fs.existsSync(file) ? fs.readFileSync(file, 'utf8') : '';
      const next = appendWorkManagerRules(appendSlf4jRule(current));
      if (next !== current) {
        fs.mkdirSync(path.dirname(file), { recursive: true });
        fs.writeFileSync(file, next);
      }
      return config;
    },
  ]);
}

module.exports = withAndroidR8Rules;
module.exports.appendSlf4jRule = appendSlf4jRule;
module.exports.appendWorkManagerRules = appendWorkManagerRules;
module.exports.RULE = RULE;
module.exports.WORK_MANAGER_RULES = WORK_MANAGER_RULES;
