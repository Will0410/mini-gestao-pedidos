using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pedidos.Domain.Entities;

namespace Pedidos.Infrastructure.Persistence;

/// <summary>
/// Apenas MAPEIA o schema criado por database/init/01-schema.sql (docs/plan.md A1).
/// Não use EnsureCreated/Migrations: o schema é dono dos scripts SQL.
/// </summary>
public sealed class PedidosDbContext(DbContextOptions<PedidosDbContext> options) : DbContext(options)
{
    public DbSet<Cliente> Clientes => Set<Cliente>();

    public DbSet<Produto> Produtos => Set<Produto>();

    public DbSet<Pedido> Pedidos => Set<Pedido>();

    public DbSet<ItemPedido> ItensPedido => Set<ItemPedido>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // DATETIME2 não guarda o Kind: marca como UTC na leitura para serializar com "Z".
        var utc = new ValueConverter<DateTime, DateTime>(v => v, v => DateTime.SpecifyKind(v, DateTimeKind.Utc));

        modelBuilder.Entity<Cliente>(e =>
        {
            e.ToTable("Clientes");
            e.HasKey(x => x.Id);
            e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            e.Property(x => x.Email).HasMaxLength(254).IsRequired();
            e.Property(x => x.DataCadastro).HasColumnType("datetime2(0)").HasConversion(utc).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Produto>(e =>
        {
            e.ToTable("Produtos");
            e.HasKey(x => x.Id);
            e.Property(x => x.Sku).HasMaxLength(30).IsUnicode(false).IsRequired();
            e.Property(x => x.Nome).HasMaxLength(150).IsRequired();
            e.Property(x => x.Preco).HasPrecision(18, 2);
        });

        modelBuilder.Entity<Pedido>(e =>
        {
            e.ToTable("Pedidos");
            e.HasKey(x => x.Id);
            e.Property(x => x.DataPedido).HasColumnType("datetime2(0)").HasConversion(utc);
            e.Property(x => x.ValorTotal).HasPrecision(18, 2);
            e.HasOne<Cliente>().WithMany().HasForeignKey(x => x.ClienteId);
            e.HasMany(x => x.Itens).WithOne().HasForeignKey(x => x.PedidoId);
            e.Navigation(x => x.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
        });

        modelBuilder.Entity<ItemPedido>(e =>
        {
            e.ToTable("ItensPedido");
            e.HasKey(x => x.Id);
            e.Property(x => x.PrecoUnitario).HasPrecision(18, 2);
            e.Ignore(x => x.Subtotal);
            e.HasOne<Produto>().WithMany().HasForeignKey(x => x.ProdutoId);
        });
    }
}
