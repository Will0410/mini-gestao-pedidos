---
name: new-endpoint
description: Cria um novo endpoint HTTP ponta a ponta seguindo o padrão do projeto (spec → contrato → validador → handler → repositório → endpoint Minimal API → testes unitários e de integração → tipo no frontend). Use quando o usuário pedir "novo endpoint", "nova rota", "expor X na API".
argument-hint: "<MÉTODO> <rota> — <descrição curta>"
---

# Skill: novo endpoint no padrão do projeto

Entrada: `$ARGUMENTS` (ex.: `GET /produtos/{id} — detalhe do produto`).

Siga **na ordem**. Não pule etapas; se uma etapa não se aplicar, diga explicitamente por quê.

## 1. Especificação primeiro
- Abra `docs/spec.md`. Adicione a rota na §5 com request, response, status codes e `code` de erro.
- Se houver regra nova, crie `RN-xx`; crie ao menos um `CA-xx` com o teste que o cobre.

## 2. Contrato (Application)
- `backend/src/Pedidos.Application/<Feature>/Contracts.cs`: `public sealed record XxxRequest(...)` e `XxxResponse(...)`.
- Nunca exponha entidades de Domain.

## 3. Validação (só se houver entrada)
- `backend/src/Pedidos.Application/<Feature>/XxxValidator.cs` herdando `AbstractValidator<XxxRequest>`.
- Mensagens em português; limites explícitos (tamanho de página, intervalos de data etc.).

## 4. Caso de uso
- Escrita: `XxxHandler` com `Task<Result<XxxResponse>> HandleAsync(XxxRequest, CancellationToken)`.
  - Erros de negócio via `Errors.*` (adicione em `Application/Common/Errors.cs` se for novo). **Nada de exceptions.**
  - Escritas múltiplas dentro de `IUnitOfWork.ExecutarEmTransacaoAsync`.
- Leitura: método em um `IXxxQueries` (interface na Application, implementação na Infrastructure) com `AsNoTracking()` + `Select` para o DTO.
- Registre no `Application/DependencyInjection.cs` (ou `Infrastructure/DependencyInjection.cs`).

## 5. Endpoint (Api)
- Em `backend/src/Pedidos.Api/Endpoints/<Feature>Endpoints.cs`, dentro de `MapXxxEndpoints`:
  ```csharp
  group.MapGet("/{id:int}", async (int id, IXxxQueries q, CancellationToken ct) =>
          (await q.ObterAsync(id, ct)).ToHttpResult())
       .WithName("ObterXxx")
       .Produces<XxxResponse>()
       .ProducesProblem(StatusCodes.Status404NotFound);
  ```
- Requests com corpo: use `.AddEndpointFilter<ValidationFilter<XxxRequest>>()`.
- Se for grupo novo, registre em `Program.cs` (`app.MapXxxEndpoints()`).

## 6. Testes
- Unitário (`backend/tests/Pedidos.UnitTests/<Feature>/`): validador e handler com os fakes de `Fakes/`. Nomeie `Metodo_Cenario_ResultadoEsperado` e cite o `CA-xx` em comentário.
- Integração (`backend/tests/Pedidos.IntegrationTests/<Feature>/`): use `[Collection(SqlServerCollection.Name)]` e `ApiFactory`; verifique status, corpo e efeito no banco.

## 7. Frontend (se a tela consumir)
- Tipo em `frontend/src/api/types.ts`, função em `frontend/src/api/client.ts`, hook em `src/features/<feature>/queries.ts`.

## 8. Verificação final
Rode e mostre o resultado:
```bash
dotnet build backend/Pedidos.sln
dotnet test backend/tests/Pedidos.UnitTests
dotnet test backend/tests/Pedidos.IntegrationTests --filter "FullyQualifiedName~<Feature>"
```
Depois proponha a mensagem de commit (`feat(api): ...`).
