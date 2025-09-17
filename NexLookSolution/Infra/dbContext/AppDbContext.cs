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

            base.OnModelCreating(modelBuilder);
        }
    }
}
