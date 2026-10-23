import type { Metadata } from 'next';
import type { Country } from '@situ/shared';
import { messageFromUnknown } from '@situ/shared';
import { RegisterForm } from '@/components/register-form';
import { api } from '@/lib/api';

export const metadata: Metadata = { title: 'Registro' };
export const dynamic = 'force-dynamic';

export default async function RegisterPage() {
  let countries: Country[] = [];
  let loadError: string | null = null;

  try {
    const response = await api.listCountries();
    countries = response.countries.filter((country) => country.isEnabled);
  } catch (cause) {
    loadError = messageFromUnknown(cause);
  }

  return <RegisterForm countries={countries} loadError={loadError} />;
}
