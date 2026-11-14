import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, beforeAll, describe, expect, it, vi } from 'vitest';
import { NovoPedidoModal } from './NovoPedidoModal';

// jsdom não implementa <dialog>.showModal: simula abrir/fechar.
beforeAll(() => {
  HTMLDialogElement.prototype.showModal = function () { this.open = true; };
  HTMLDialogElement.prototype.close = function () { this.open = false; };
});

afterEach(() => vi.unstubAllGlobals());

const json = (status: number, body: unknown) =>
  Promise.resolve(new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } }));

function setup(postResponse: () => Promise<Response>) {
  const fetchMock = vi.fn((url: string, init?: RequestInit) => {
    if (url.endsWith('/clientes')) return json(200, [{ id: 1, nome: 'Ana Souza', email: 'ana@x.com' }]);
    if (url.endsWith('/produtos')) return json(200, [{ id: 29, sku: 'ED', nome: 'Edição Limitada', preco: 999.99, estoque: 3 }]);
    if (url.endsWith('/pedidos') && init?.method === 'POST') return postResponse();
    return json(404, {});
  });
  vi.stubGlobal('fetch', fetchMock);
  const onCriado = vi.fn();
  render(
    <QueryClientProvider client={new QueryClient({ defaultOptions: { queries: { retry: false } } })}>
      <NovoPedidoModal aberto onFechar={() => {}} onCriado={onCriado} />
    </QueryClientProvider>,
  );
  return { fetchMock, onCriado };
}

async function preencher(quantidade: string) {
  const user = userEvent.setup();
  await waitFor(() => expect(screen.getByRole('option', { name: /Ana Souza/ })).toBeInTheDocument());
  await user.selectOptions(screen.getByTestId('novo-pedido-cliente'), '1');
  await waitFor(() => expect(screen.getByRole('option', { name: /Edição Limitada/ })).toBeInTheDocument());
  await user.selectOptions(screen.getByTestId('item-0-produto'), '29');
  await user.clear(screen.getByTestId('item-0-quantidade'));
  await user.type(screen.getByTestId('item-0-quantidade'), quantidade);
  return user;
}

describe('NovoPedidoModal', () => {
  it('mostra total em tempo real e envia o pedido sem preço', async () => {
    const { fetchMock, onCriado } = setup(() =>
      json(201, { id: 77, clienteId: 1, clienteNome: 'Ana Souza', dataPedido: '2026-11-14T12:00:00Z', valorTotal: 1999.98, itens: [] }),
    );
    const user = await preencher('2');

    expect(screen.getByTestId('novo-pedido-total')).toHaveTextContent('1.999,98');
    await user.click(screen.getByTestId('salvar-pedido'));

    await waitFor(() => expect(onCriado).toHaveBeenCalledWith(expect.objectContaining({ id: 77 })));
    const post = fetchMock.mock.calls.find(([, init]) => init?.method === 'POST');
    expect(JSON.parse(String(post?.[1]?.body))).toEqual({ clienteId: 1, itens: [{ produtoId: 29, quantidade: 2 }] });
  });

  it('bloqueia envio quando a quantidade passa do estoque', async () => {
    const { fetchMock } = setup(() => json(500, {}));
    const user = await preencher('5');

    await user.click(screen.getByTestId('salvar-pedido'));

    expect(await screen.findByText(/Estoque insuficiente \(disponível: 3\)/)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([, init]) => init?.method === 'POST')).toBe(false);
  });

  it('exibe a mensagem de negócio retornada pela API (409)', async () => {
    setup(() =>
      json(409, { status: 409, code: 'ESTOQUE_INSUFICIENTE', detail: "Estoque insuficiente para 'Edição Limitada': solicitado 2, disponível 1." }),
    );
    const user = await preencher('2');

    await user.click(screen.getByTestId('salvar-pedido'));

    expect(await screen.findByTestId('erro-api')).toHaveTextContent('disponível 1');
  });
});
