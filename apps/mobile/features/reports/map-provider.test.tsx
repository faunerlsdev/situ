import { describe, expect, it } from 'vitest';

import { androidMapsKeyVariable, resolveMapProvider } from './map-provider';

describe('resolveMapProvider', () => {
  it('uses Apple Maps on iOS when the iOS key is missing', () => {
    expect(resolveMapProvider('ios', { ios: '', android: 'android-key' })).toEqual({
      status: 'apple',
      userInterfaceStyle: 'dark',
    });
  });

  it('uses Google Maps on iOS when the iOS key is set', () => {
    expect(resolveMapProvider('ios', { ios: ' ios-key ', android: null })).toEqual({ status: 'google' });
  });

  it('uses Google Maps on Android when the Android key is set', () => {
    expect(resolveMapProvider('android', { ios: null, android: 'android-key' })).toEqual({ status: 'google' });
  });

  it('does not offer a map on Android when the Android key is missing', () => {
    expect(resolveMapProvider('android', { ios: 'ios-key', android: '  ' })).toEqual({
      status: 'unavailable',
      missingVariable: androidMapsKeyVariable,
    });
  });
});
