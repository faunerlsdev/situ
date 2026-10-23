import type { ComponentPropsWithoutRef } from 'react';
import { compileMDX } from 'next-mdx-remote/rsc';

const components = {
  h2: (props: ComponentPropsWithoutRef<'h2'>) => <h2 className="mt-8 font-display text-2xl" {...props} />,
  p: (props: ComponentPropsWithoutRef<'p'>) => <p className="mt-4 leading-8 text-gray600" {...props} />,
  ul: (props: ComponentPropsWithoutRef<'ul'>) => <ul className="mt-4 list-disc space-y-2 pl-5 leading-8 text-gray600" {...props} />,
  a: (props: ComponentPropsWithoutRef<'a'>) => (
    <a className="font-semibold text-ink underline-offset-2 hover:underline" {...props} />
  ),
};

export async function ArticleBody({ source }: { source: string }) {
  const { content } = await compileMDX({ source, components });
  return <div>{content}</div>;
}
