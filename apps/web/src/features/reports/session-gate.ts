export function sessionBlock(signedIn: boolean): string | null {
  return signedIn ? null : 'Inicia sesión para ver el reporte, confirmarlo, comentarlo o denunciarlo.';
}

export function toggleCategory(selected: readonly string[], code: string) {
  return selected.includes(code) ? selected.filter((item) => item !== code) : [...selected, code];
}
