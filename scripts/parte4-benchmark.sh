#!/usr/bin/env bash
# Executa o benchmark da Parte 4 no SQL Server do docker compose e mostra só o essencial.
set -euo pipefail
cd "$(dirname "$0")/.."
export MSYS_NO_PATHCONV=1
docker compose exec -T sqlserver /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "${SA_PASSWORD:-Pedidos@Local2026!}" -C -d PedidosDb -W -s '|' \
  -i /dev/stdin < database/queries/parte4-benchmark.sql
