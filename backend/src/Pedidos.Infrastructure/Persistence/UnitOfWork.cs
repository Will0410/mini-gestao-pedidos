using System.Data;
using Microsoft.EntityFrameworkCore;
using Pedidos.Application.Abstractions;
using Pedidos.Domain.Common;

namespace Pedidos.Infrastructure.Persistence;

internal sealed class UnitOfWork(PedidosDbContext db) : IUnitOfWork
{
    public async Task<Result<T>> ExecutarEmTransacaoAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operacao,
        CancellationToken ct)
    {
        // A execution strategy envolve a transação inteira: se a política de retry estiver ligada,
        // a unidade completa é repetida (e não só um comando no meio da transação).
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async token =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, token);
            try
            {
                var resultado = await operacao(token);
                if (resultado.IsFailure)
                {
                    await tx.RollbackAsync(token);
                    db.ChangeTracker.Clear();
                    return resultado;
                }

                await db.SaveChangesAsync(token);
                await tx.CommitAsync(token);
                return resultado;
            }
            catch
            {
                await tx.RollbackAsync(CancellationToken.None);
                db.ChangeTracker.Clear();
                throw;
            }
        }, ct);
    }
}
