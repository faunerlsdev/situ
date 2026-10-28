import { absoluteMediaUrl, defaultApiUrl, readApiUrl } from '@situ/shared';
import { useState } from 'react';
import { Image } from 'react-native';
import type { ImageStyle, StyleProp } from 'react-native';

import { localCategoryIcon } from '@/features/reports/category-icon-assets';
import { categoryIconSource, categorySlug } from '@/features/reports/category-icons';

const generic = require('../../assets/images/icons/situ-pin-mapa-dark.png');
const apiBase = readApiUrl(process.env.EXPO_PUBLIC_API_URL, defaultApiUrl);

export function CategoryMark({
  code,
  remotePath,
  kind,
  style,
}: {
  code: string | null;
  remotePath: string | null;
  kind: 'button' | 'map';
  style: StyleProp<ImageStyle>;
}) {
  const [failed, setFailed] = useState(false);
  const remoteUrl = absoluteMediaUrl(apiBase, remotePath);
  const local = localCategoryIcon(categorySlug(code), kind);
  const source = categoryIconSource({ remoteUrl, remoteFailed: failed, hasLocal: local !== null });
  if (source === 'remote' && remoteUrl) {
    return <Image source={{ uri: remoteUrl }} style={style} resizeMode="contain" onError={() => setFailed(true)} />;
  }
  if (source === 'local' && local) {
    return <Image source={local} style={style} resizeMode="contain" />;
  }
  return <Image source={generic} style={style} resizeMode="contain" />;
}
