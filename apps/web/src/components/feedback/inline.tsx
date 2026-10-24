export function InlineAlert({ message }: { message: string }) {
  return (
    <p role="alert" className="flex items-start gap-2 text-[13px] leading-5 font-bold text-app-danger">
      <span aria-hidden="true" className="mt-0.5 inline-grid size-4 shrink-0 place-items-center rounded-full bg-live text-[10px] text-white">
        !
      </span>
      {message}
    </p>
  );
}

export function FieldError({ id, message }: { id: string; message: string }) {
  return (
    <p id={id} className="mt-1 flex items-start gap-2 text-[13px] leading-5 font-bold text-app-danger">
      <span aria-hidden="true">!</span>
      {message}
    </p>
  );
}

export function QueryError({ message, onRetry }: { message: string; onRetry: () => void }) {
  return (
    <div role="alert" className="grid justify-items-center gap-3 rounded-[12px] bg-app-surface px-4 py-6 text-center">
      <img src="/brand/situ-isotipo-naranja-claro.png" alt="" className="h-16 w-12 object-contain" />
      <p className="font-sans text-body text-app-text">{message}</p>
      <button type="button" onClick={onRetry} className="font-sans text-body font-bold text-primary">
        Reintentar
      </button>
    </div>
  );
}

export function SkeletonCards() {
  return (
    <div aria-hidden="true" className="space-y-2">
      <div className="h-16 animate-pulse rounded-[12px] bg-app-input" />
      <div className="h-16 animate-pulse rounded-[12px] bg-app-input" />
      <div className="h-16 animate-pulse rounded-[12px] bg-app-input" />
    </div>
  );
}

export function AreaProgress({ active }: { active: boolean }) {
  if (!active) {
    return null;
  }

  return <div aria-hidden="true" className="h-0.5 w-full overflow-hidden bg-primary/30"><span className="situ-progress block h-full w-1/3 bg-primary" /></div>;
}

export function UploadProgress({ percent, onCancel }: { percent: number; onCancel: () => void }) {
  return (
    <div className="rounded-[12px] bg-app-surface p-3">
      <div className="mb-2 flex items-center justify-between font-sans text-small text-app-text">
        <span>Subiendo {percent}%</span>
        <button type="button" onClick={onCancel} className="font-bold text-primary">
          Cancelar
        </button>
      </div>
      <div className="h-1 overflow-hidden rounded-full bg-app-border">
        <div className="h-full bg-primary" style={{ width: `${percent}%` }} />
      </div>
    </div>
  );
}
