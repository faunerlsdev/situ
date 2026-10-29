declare module '../../../../scripts/legal/fill-terminos.mjs' {
  export type TerminosIdentity = {
    responsibleName: string;
    supportEmail: string;
    nationalId: string;
    phone: string;
    physicalAddress: string;
  };

  export function fillTerminos(source: string, identity: TerminosIdentity): string;
  export function terminosProblems(filled: string): string[];
}
