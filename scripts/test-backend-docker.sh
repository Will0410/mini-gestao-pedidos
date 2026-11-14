#!/usr/bin/env bash
# Roda testes unitários + de integração do backend DENTRO de um container do SDK .NET,
# usando o SQL Server do docker compose (rede "pedidos_default").
#
# Útil quando a máquina não tem o .NET SDK ou quando políticas de segurança do sistema
# (ex.: Windows Application Control) bloqueiam executáveis recém-compilados. Só precisa do Docker.
#
# Uso:  ./scripts/test-backend-docker.sh            (sobe o banco se necessário)
set -euo pipefail
cd "$(dirname "$0")/.."
export MSYS_NO_PATHCONV=1 # Git Bash no Windows: não converter caminhos tipo /src

docker compose up -d sqlserver >/dev/null
echo "[tests] aguardando SQL Server ficar saudável..."
until [ "$(docker inspect -f '{{.State.Health.Status}}' "$(docker compose ps -q sqlserver)")" = "healthy" ]; do sleep 2; done

docker run --rm \
  --network pedidos_default \
  -v "$(pwd)":/repo:ro \
  -v pedidos-nuget:/root/.nuget/packages \
  -e INTEGRATION_SQL_SERVER=sqlserver \
  -e INTEGRATION_SQL_SA_PASSWORD="${SA_PASSWORD:-Pedidos@Local2026!}" \
  -e INTEGRATION_APP_DB_PASSWORD="${APP_DB_PASSWORD:-App@Pedidos2026!}" \
  -e INTEGRATION_MCP_DB_PASSWORD="${MCP_DB_PASSWORD:-Mcp@ReadOnly2026!}" \
  -e DOTNET_CLI_TELEMETRY_OPTOUT=1 \
  mcr.microsoft.com/dotnet/sdk:10.0 \
  bash -c '
    set -e
    mkdir -p /work && cd /repo
    # Copia só o código-fonte (sem bin/obj do host, que têm caminhos do Windows).
    tar --exclude="bin" --exclude="obj" -cf - backend database | tar -xf - -C /work
    cd /work/backend
    dotnet test --project tests/Pedidos.UnitTests
    dotnet test --project tests/Pedidos.IntegrationTests
  '
