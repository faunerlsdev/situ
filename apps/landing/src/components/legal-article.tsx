import type { LegalDocument } from '@/lib/legal-document';
import { legalReviewBanner } from '@/lib/legal-document';

export function LegalArticle({ document }: { document: LegalDocument }) {
  return (
    <article className="mx-auto w-full max-w-3xl px-5 py-16">
      {document.placeholder ? (
        <p role="status" className="mb-6 rounded-2xl border border-amber-300 bg-amber-50 px-4 py-3 font-semibold text-ink">
          {legalReviewBanner}
        </p>
      ) : null}
      <h1 className="font-display text-5xl">{document.title}</h1>
      <p className="mt-3 text-sm text-gray600">
        Versión {document.version} · {document.updated}
      </p>
      <div className="mt-6 space-y-4 leading-8 text-gray600">
        {document.blocks.map((block, index) => {
          if (block.kind === 'heading') {
            return (
              <h2 key={index} className="font-display text-2xl text-ink">
                {block.text}
              </h2>
            );
          }
          if (block.kind === 'item') {
            return <p key={index}>{block.text}</p>;
          }
          return <p key={index}>{block.text}</p>;
        })}
      </div>
    </article>
  );
}
