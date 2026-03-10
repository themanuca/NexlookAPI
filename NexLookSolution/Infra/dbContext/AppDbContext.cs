using Domain.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Infra.dbContext
{
    public class AppDbContext: DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Look> Looks { get; set; }
        public DbSet<LookImage> LookImages { get; set; }
        public DbSet<Credito> Creditos { get; set; }
        public DbSet<Subscricao> Subscricoes { get; set; }
        public DbSet<Plano> Planos { get; set; }
        public DbSet<Pagamento> Pagamentos { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Usuario - Subscricao
            modelBuilder.Entity<Subscricao>()
                .HasOne(s => s.Usuario)
                .WithMany(u => u.Subscriptions)
                .HasForeignKey(s => s.UsuarioId);

            // Usuario - Look
            modelBuilder.Entity<Look>()
                .HasOne(l => l.Usuario)
                .WithMany(u => u.Looks)
                .HasForeignKey(l => l.UsuarioId);

            // Usuario - Credito
            modelBuilder.Entity<Credito>()
                .HasOne(c => c.Usuario)
                .WithMany()
                .HasForeignKey(c => c.UsuarioId);

            // Subscricao - Plano
            modelBuilder.Entity<Subscricao>()
                .HasOne(s => s.Plano)
                .WithMany(p => p.Subscriptions)
                .HasForeignKey(s => s.PlanoId);

            // Subscricao - Pagamento
            modelBuilder.Entity<Pagamento>()
                .HasOne(p => p.Subscricao)
                .WithMany(s => s.Payments)
                .HasForeignKey(p => p.SubscricaoId);

            // Look - LookImage
            modelBuilder.Entity<LookImage>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.ImageUrl)
                    .IsRequired();

                // Configurando as novas propriedades
                entity.Property(e => e.PublicIdCloudnary)
                    .HasMaxLength(255); // Ajuste o tamanho conforme necessário

                entity.Property(e => e.PublicIdFirebase)
                    .HasMaxLength(255); // Ajuste o tamanho conforme necessário

                entity.HasOne(e => e.Look)
                    .WithMany(e => e.Images)
                    .HasForeignKey(e => e.LookId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // Seed: planos padrão
            modelBuilder.Entity<Plano>().HasData(
                new Plano
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                    Nome = "Free",
                    Preco = 0m,
                    DuracaoEmDias = 36500, // ~100 anos (sem expiração)
                    Descricao = "Plano gratuito com acesso básico.",
                    IsAtivo = true,
                    LimitePecas = 15,
                    LimiteGeracoes = 3
                },
                new Plano
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000002"),
                    Nome = "Pro",
                    Preco = 19m,
                    DuracaoEmDias = 30,
                    Descricao = "Até 50 peças no armário e 30 gerações de look por mês.",
                    IsAtivo = true,
                    LimitePecas = 50,
                    LimiteGeracoes = 30
                },
                new Plano
                {
                    Id = Guid.Parse("00000000-0000-0000-0000-000000000003"),
                    Nome = "Premium",
                    Preco = 39m,
                    DuracaoEmDias = 30,
                    Descricao = "Peças e gerações ilimitadas.",
                    IsAtivo = true,
                    LimitePecas = -1,
                    LimiteGeracoes = -1
                }
            );

            base.OnModelCreating(modelBuilder);
        }
    }
}
