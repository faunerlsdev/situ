export function FormStatus({ error, notice }: { error: string | null; notice: string | null }) {
  if (error) {
    return (
      <p role="alert" className="flex items-start gap-2 rounded-[12px] bg-white px-3 py-2 text-[13px] font-bold text-live">
        <span aria-hidden="true">!</span>
        {error}
      </p>
    );
  }

  if (notice) {
    return (
      <p role="status" className="rounded-xl border border-ink bg-white px-3 py-3 text-sm text-ink">
        {notice}
      </p>
    );
  }

  return null;
}

export function readField(form: FormData, name: string): string {
  const value = form.get(name);
  return typeof value === 'string' ? value.trim() : '';
}

export const controlClassName = 'mt-2 w-full rounded-xl border border-gray200 bg-white px-3 py-3 text-ink';
