export const fallbackResponseCommitment =
  'Revisamos cada denuncia. Si el contenido afecta a un menor, lo ocultamos de inmediato. El resto lo atendemos en menos de 24 horas.';

export function responseCommitment(value: string | null | undefined): string {
  const text = value?.trim();
  return text && text.length > 0 ? text : fallbackResponseCommitment;
}
