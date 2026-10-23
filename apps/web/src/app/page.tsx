'use client';

import { useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';

export default function HomePage() {
  const router = useRouter();
  const [visible, setVisible] = useState(false);

  useEffect(() => {
    const frame = requestAnimationFrame(() => setVisible(true));
    const go = window.setTimeout(() => router.push('/login'), 400);
    return () => {
      cancelAnimationFrame(frame);
      window.clearTimeout(go);
    };
  }, [router]);

  return (
    <div className="grid min-h-dvh place-items-center bg-[radial-gradient(circle,var(--color-edge-center)_0%,var(--color-edge)_72%)]">
      <img
        src="/brand/situ-logo-vertical-naranja.png"
        alt="SITU"
        width={130}
        height={142}
        className={`w-[130px] object-contain transition-opacity duration-[400ms] ${visible ? 'opacity-100' : 'opacity-0'}`}
      />
    </div>
  );
}
