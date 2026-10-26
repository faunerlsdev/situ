import { test } from '@playwright/test';

test('ingreso con segundo factor y resumen', async ({ page }) => {
  test.skip(
    process.env.E2E_ADMIN !== '1',
    'Requiere la API local reiniciada, un superadministrador con TOTP y E2E_ADMIN=1. No se ejecutó en esta entrega.',
  );
  await page.goto('/login');
  await page.getByLabel('Correo').fill(process.env.E2E_ADMIN_EMAIL ?? '');
  await page.getByLabel('Contraseña').fill(process.env.E2E_ADMIN_PASSWORD ?? '');
  await page.getByRole('button', { name: 'Ingresar' }).click();
  await page.getByLabel('Código').fill(process.env.E2E_ADMIN_CODE ?? '');
  await page.getByRole('button', { name: 'Confirmar' }).click();
  await page.getByRole('heading', { name: 'Resumen' }).waitFor();
});
