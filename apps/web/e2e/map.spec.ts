import { expect, test } from '@playwright/test';

test('el mapa muestra la pantalla aunque falte la clave de Google', async ({ page }) => {
  await page.goto('/mapa');
  await expect(page.getByRole('heading', { name: 'Falta la clave del mapa' })).toBeVisible();
});

test('el ingreso abre el formulario', async ({ page }) => {
  await page.goto('/login');
  await expect(page.getByLabel('Usuario')).toBeVisible();
  await expect(page.getByLabel('Contraseña')).toBeVisible();
});
