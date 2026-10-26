export function EmailReveal({ email, canReveal, onReveal }: { email: string; canReveal: boolean; onReveal: () => void }) {
  return (
    <p className="flex flex-wrap items-center gap-3">
      <span className="break-all">{email}</span>
      {canReveal ? (
        <button type="button" onClick={onReveal} className="text-sm font-semibold text-app-primary">
          Ver correo completo
        </button>
      ) : null}
    </p>
  );
}
