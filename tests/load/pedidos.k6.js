// Teste de carga (k6) — valida RNF-02: GET /pedidos p95 < 200 ms com 50 VUs e 20k pedidos.
//
// Rodar contra o compose (rede interna, sem passar pelo host):
//   docker run --rm -i --network pedidos_default -e BASE_URL=http://api:8080 grafana/k6:2.4.0 run - < tests/load/pedidos.k6.js
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5080';

const listar = new Trend('lat_listar_pedidos', true);
const relatorio = new Trend('lat_top_clientes', true);
const criar = new Trend('lat_criar_pedido', true);
const errosNegocio = new Rate('erros_inesperados');

export const options = {
  scenarios: {
    // Leitura: rampa até 50 usuários navegando na listagem/relatório.
    leitura: {
      executor: 'ramping-vus',
      exec: 'leitura',
      startVUs: 0,
      stages: [
        { duration: '15s', target: 50 },
        { duration: '45s', target: 50 },
        { duration: '10s', target: 0 },
      ],
    },
    // Escrita: 5 pedidos/s constantes (abaixo do rate limit de 100/10s por IP).
    escrita: {
      executor: 'constant-arrival-rate',
      exec: 'escrita',
      rate: 5,
      timeUnit: '1s',
      duration: '60s',
      preAllocatedVUs: 10,
    },
  },
  thresholds: {
    'lat_listar_pedidos': ['p(95)<200'], // RNF-02
    'lat_top_clientes': ['p(95)<300'],
    'lat_criar_pedido': ['p(95)<300'],
    'erros_inesperados': ['rate<0.01'],
    'http_req_failed{scenario:leitura}': ['rate<0.01'],
  },
};

const rnd = (min, max) => Math.floor(Math.random() * (max - min + 1)) + min;
const pad = (n) => String(n).padStart(2, '0');

export function leitura() {
  // Mistura realista: sem filtro, por cliente, por período, por cliente + período, páginas variadas.
  const mes = rnd(1, 10);
  const filtros = [
    `page=${rnd(1, 50)}&pageSize=20`,
    `clienteId=${rnd(1, 22)}`,
    `dataInicio=2026-${pad(mes)}-01&dataFim=2026-${pad(mes)}-28`,
    `clienteId=${rnd(1, 22)}&dataInicio=2025-01-01&dataFim=2026-12-31&page=${rnd(1, 5)}`,
  ];
  const r = http.get(`${BASE_URL}/pedidos?${filtros[rnd(0, filtros.length - 1)]}`, { tags: { name: 'GET /pedidos' } });
  listar.add(r.timings.duration);
  check(r, { 'listar 200': (x) => x.status === 200 });
  errosNegocio.add(r.status !== 200);

  if (Math.random() < 0.2) {
    const t = http.get(`${BASE_URL}/relatorios/top-clientes?dataInicio=2026-01-01&dataFim=2026-12-31`, {
      tags: { name: 'GET /relatorios/top-clientes' },
    });
    relatorio.add(t.timings.duration);
    check(t, { 'top 200': (x) => x.status === 200 });
    errosNegocio.add(t.status !== 200);
  }
  sleep(Math.random() * 0.5);
}

export function escrita() {
  const body = JSON.stringify({
    clienteId: rnd(1, 22),
    itens: [
      { produtoId: rnd(1, 14), quantidade: rnd(1, 3) },
      { produtoId: rnd(15, 28), quantidade: rnd(1, 3) },
    ],
  });
  const r = http.post(`${BASE_URL}/pedidos`, body, {
    headers: { 'Content-Type': 'application/json' },
    tags: { name: 'POST /pedidos' },
  });
  criar.add(r.timings.duration);
  check(r, { 'criar 201': (x) => x.status === 201 });
  errosNegocio.add(r.status !== 201);
}
