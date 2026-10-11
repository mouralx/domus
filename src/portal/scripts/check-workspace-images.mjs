import { expect } from '@playwright/test';
export async function checkWorkspaceImages(browser) {
  const page = await browser.newPage({ viewport: { width: 1440, height: 1050 } });
  await page.goto('http://localhost:4173');
  await page.getByRole('button', { name: 'Settings', exact: true }).click();
  await page.getByRole('button', { name: 'Workspace', exact: true }).click();
  await page.getByRole('button', { name: 'Workspace appearance', exact: true }).click();
  const image = await page.evaluate(() => {
    const canvas = document.createElement('canvas');
    canvas.width = canvas.height = 64;
    const context = canvas.getContext('2d');
    context.fillStyle = '#167d8d';
    context.fillRect(0, 0, 64, 64);
    return canvas.toDataURL('image/png').split(',')[1];
  });
  await page.getByLabel('Workspace icon', { exact: true }).setInputFiles({
    name: 'icon.png',
    mimeType: 'image/png',
    buffer: Buffer.from(image, 'base64'),
  });
  await expect(page.getByAltText('Workspace icon preview')).toBeVisible();
  await page.getByRole('button', { name: 'Save workspace images', exact: true }).click();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await expect(page.getByAltText('Workspace icon', { exact: true }).first()).toBeVisible();
  console.log('PASS: workspace icon upload, preview and save.');
  await page.close();
}
