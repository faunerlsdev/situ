import type { ReactNode } from 'react';
import type { PrivacyBlock, PrivacyDocument } from '@/lib/privacy-policy';

export function PrivacyArticle({ document }: { document: PrivacyDocument }) {
  return (
    <article className="mx-auto w-full max-w-3xl px-5 py-16">
      <h1 className="font-display text-4xl leading-tight sm:text-5xl">{document.title}</h1>
      <div className="mt-6 space-y-4 leading-8 text-gray600">{document.intro.map((block, index) => renderBlock(block, `intro-${index}`))}</div>
      {document.sections.length > 0 ? (
        <nav aria-label="Contenido" className="mt-10 rounded-3xl border border-gray200 bg-white p-5">
          <p className="font-display text-2xl text-ink">Contenido</p>
          <ol className="mt-4 space-y-2 text-sm">
            {document.sections.map((section) => (
              <li key={section.id}>
                <a href={`#${section.id}`} className="font-semibold text-ink underline-offset-2 hover:underline">
                  {section.title}
                </a>
              </li>
            ))}
          </ol>
        </nav>
      ) : null}
      {document.sections.map((section) => (
        <section key={section.id} id={section.id} className="mt-12 scroll-mt-6">
          <h2 className="font-display text-2xl text-ink">{section.title}</h2>
          <div className="mt-4 space-y-4 leading-8 text-gray600">{section.blocks.map((block, index) => renderBlock(block, `${section.id}-${index}`))}</div>
        </section>
      ))}
    </article>
  );
}

function renderBlock(block: PrivacyBlock, key: string) {
  if (block.kind === 'subheading') {
    return (
      <h3 key={key} id={block.id} className="font-display text-xl text-ink">
        {block.text}
      </h3>
    );
  }
  if (block.kind === 'list') {
    const ListTag = block.ordered ? 'ol' : 'ul';
    return (
      <ListTag key={key} className={block.ordered ? 'list-decimal space-y-2 pl-5' : 'list-disc space-y-2 pl-5'}>
        {block.items.map((item) => (
          <li key={item}>{renderInline(item)}</li>
        ))}
      </ListTag>
    );
  }
  if (block.kind === 'table') {
    return (
      <div key={key}>
        <div className="space-y-3 sm:hidden">
          {block.rows.map((row) => (
            <div key={row.join('|')} className="rounded-2xl border border-gray200 bg-white p-4">
              {row.map((cell, index) => {
                const header = block.headers[index] ?? '';
                return (
                  <p key={`${header}-${index}`} className={index === 0 ? 'font-semibold text-ink' : 'mt-1'}>
                    <span className="block text-xs uppercase tracking-wide text-gray600">{header}</span>
                    {renderInline(cell)}
                  </p>
                );
              })}
            </div>
          ))}
        </div>
        <div className="hidden overflow-x-auto sm:block">
          <table className="w-full border-collapse text-left text-sm">
            <thead>
              <tr>
                {block.headers.map((header) => (
                  <th key={header} className="border-b border-gray200 px-3 py-2 font-semibold text-ink">
                    {renderInline(header)}
                  </th>
                ))}
              </tr>
            </thead>
            <tbody>
              {block.rows.map((row) => (
                <tr key={row.join('|')}>
                  {row.map((cell, index) => (
                    <td key={`${cell}-${index}`} className="border-b border-gray200 px-3 py-3 align-top">
                      {renderInline(cell)}
                    </td>
                  ))}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>
    );
  }
  return <p key={key}>{renderInline(block.text)}</p>;
}

function renderInline(text: string): ReactNode[] {
  const nodes: ReactNode[] = [];
  const pattern = /(\*\*[^*]+\*\*|https?:\/\/[^\s)]+)/g;
  let cursor = 0;
  for (const match of text.matchAll(pattern)) {
    const value = match[0];
    const start = match.index ?? 0;
    if (start > cursor) {
      nodes.push(text.slice(cursor, start));
    }
    if (value.startsWith('**')) {
      nodes.push(<strong key={`${start}-b`} className="font-semibold text-ink">{value.slice(2, -2)}</strong>);
    } else {
      const href = value.replace(/[.,;:]+$/, '');
      const trailing = value.slice(href.length);
      nodes.push(
        <a key={`${start}-a`} href={href} className="font-semibold text-ink underline-offset-2 hover:underline">
          {href}
        </a>,
      );
      if (trailing) {
        nodes.push(trailing);
      }
    }
    cursor = start + value.length;
  }
  if (cursor < text.length) {
    nodes.push(text.slice(cursor));
  }
  return nodes;
}
