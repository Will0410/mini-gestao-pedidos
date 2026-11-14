#!/bin/bash
# Executado pelo serviço db-init do docker-compose.
# Espera o SQL Server aceitar conexões e roda database/init/*.sql em ordem numérica.
set -euo pipefail

SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
HOST="${DB_HOST:-sqlserver}"
DB_NAME="${DB_NAME:-PedidosDb}"
COMMON=(-S "$HOST" -U sa -P "$SA_PASSWORD" -C -b -I)
VARS=(-v DB_NAME="$DB_NAME" APP_DB_PASSWORD="$APP_DB_PASSWORD" MCP_DB_PASSWORD="$MCP_DB_PASSWORD")

echo "[db-init] aguardando SQL Server em $HOST..."
for i in $(seq 1 60); do
  if "$SQLCMD" "${COMMON[@]}" -Q "SELECT 1" > /dev/null 2>&1; then break; fi
  if [ "$i" -eq 60 ]; then echo "[db-init] SQL Server não respondeu a tempo"; exit 1; fi
  sleep 2
done

for f in /scripts/init/*.sql; do
  name="$(basename "$f")"
  # 00 roda no master (cria o banco); os demais no banco da aplicação.
  if [[ "$name" == 00-* ]]; then db="master"; else db="$DB_NAME"; fi
  echo "[db-init] executando $name (db: $db)"
  "$SQLCMD" "${COMMON[@]}" -d "$db" "${VARS[@]}" -i "$f"
done

echo "[db-init] concluído."
