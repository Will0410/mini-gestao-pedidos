import { expect, test, type Locator } from '@playwright/test';

/** Seleciona a opção cujo texto CONTÉM `nome` (o rótulo inclui o estoque, que muda a cada execução). */
async function selecionarPorTexto(select: Locator, nome: string) {
  const opcao = select.locator('option', { hasText: nome }).first();
  await expect(opcao).toBeAttached();
  const value = await opcao.getAttribute('value');
  if (!value) throw new Error(`Opção "${nome}" sem value`);
  await select.selectOption(value);
}

test.describe('Gestão de pedidos', () => {
  test('CA-14: cria um pedido no modal e o vê no topo da listagem', async ({ page }) => {
    await page.goto('/');
    await expect(page.getByTestId('tabela-pedidos')).toBeVisible();

    await page.getByTestId('novo-pedido').click();
    const modal = page.getByRole('dialog', { name: 'Novo pedido' });
    await expect(modal).toBeVisible();

    await selecionarPorTexto(modal.getByTestId('novo-pedido-cliente'), 'Bruno Lima');
    await selecionarPorTexto(modal.getByTestId('item-0-produto'), 'Mouse sem fio');
    await modal.getByTestId('item-0-quantidade').fill('2');

    await modal.getByTestId('adicionar-item').click();
    await selecionarPorTexto(modal.getByTestId('item-1-produto'), 'Hub USB-C');
    await modal.getByTestId('item-1-quantidade').fill('1');

    // 2 × 129,90 + 1 × 219,90 = 479,70
    await expect(modal.getByTestId('novo-pedido-total')).toHaveText(/479,70/);

    const respostaPromise = page.waitForResponse((r) => r.url().endsWith('/api/pedidos') && r.request().method() === 'POST');
    await modal.getByTestId('salvar-pedido').click();
    const resposta = await respostaPromise;
    expect(resposta.status()).toBe(201);
    const criado = (await resposta.json()) as { id: number };

    await expect(modal).toBeHidden();
    await expect(page.getByTestId('pedido-criado')).toContainText(`#${criado.id}`);

    // O pedido novo é o mais recente: primeira linha da tabela.
    const primeiraLinha = page.getByTestId('tabela-pedidos').locator('tbody tr').first();
    await expect(primeiraLinha).toHaveAttribute('data-testid', `pedido-${criado.id}`);
    await expect(primeiraLinha).toContainText('Bruno Lima');
    await expect(primeiraLinha).toContainText('479,70');
  });

  test('filtra a listagem por cliente e mantém o filtro na URL', async ({ page }) => {
    await page.goto('/');
    await selecionarPorTexto(page.getByTestId('filtro-cliente'), 'Ana Souza');

    await expect(page).toHaveURL(/clienteId=4/);
    const linhas = page.getByTestId('tabela-pedidos').locator('tbody tr');
    await expect(linhas.first()).toContainText('Ana Souza');
    for (const texto of await linhas.allInnerTexts()) expect(texto).toContain('Ana Souza');

    // Recarregar preserva o filtro (link compartilhável).
    await page.reload();
    await expect(page.getByTestId('filtro-cliente')).toHaveValue('4');
  });

  test('impede quantidade acima do estoque antes de chamar a API', async ({ page }) => {
    await page.goto('/');
    await page.getByTestId('novo-pedido').click();
    const modal = page.getByRole('dialog', { name: 'Novo pedido' });

    await selecionarPorTexto(modal.getByTestId('novo-pedido-cliente'), 'Carla Mendes');
    await selecionarPorTexto(modal.getByTestId('item-0-produto'), 'Notebook 14');
    await modal.getByTestId('item-0-quantidade').fill('1000');
    await modal.getByTestId('adicionar-item').click();
    await selecionarPorTexto(modal.getByTestId('item-1-produto'), 'Monitor 27');
    await modal.getByTestId('item-1-quantidade').fill('0');

    let chamouApi = false;
    page.on('request', (r) => {
      if (r.method() === 'POST' && r.url().endsWith('/api/pedidos')) chamouApi = true;
    });
    await modal.getByTestId('salvar-pedido').click();

    await expect(modal.getByText(/Quantidade entre 1 e 1000/)).toBeVisible();
    expect(chamouApi).toBe(false);
  });

  test('mostra o ranking de top clientes sem fundir homônimos', async ({ page }) => {
    await page.goto('/?dataInicio=2026-01-01&dataFim=2026-12-31');
    const painel = page.getByTestId('top-clientes');
    await expect(painel.locator('li')).toHaveCount(5);
    // Os dois clientes "Maria Silva" (ids 1 e 2) aparecem separados — bug do GROUP BY c.Nome (Parte 4).
    await expect(painel.getByText('Maria Silva')).toHaveCount(2);
  });
});
