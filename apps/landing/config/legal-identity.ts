/**
 * Datos de identificación del responsable. La página pública los oculta
 * mientras showPrivateIdentity sea false. Activar la bandera los muestra
 * sin editar el texto de la política.
 */
export const legalIdentity = {
  showPrivateIdentity: false,
  responsibleName: 'FAUNER LARA SILVA',
  supportEmail: 'soporte@somossitu.com',
  nationalId: '1.118.531.823',
  phone: '+57 321 237 1287',
  physicalAddress: 'Av. Tacuara, Prados de Valverde, Torre 6, Apto 610, Yopal, Casanare',
} as const;

export type LegalIdentity = {
  showPrivateIdentity: boolean;
  responsibleName: string;
  supportEmail: string;
  nationalId: string;
  phone: string;
  physicalAddress: string;
};
