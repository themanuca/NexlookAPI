using System;

namespace Domain.Models
{
    public class Credito
    {
        public Guid Id { get; set; }
        public Guid UsuarioId { get; set; }
        public int Quantidade { get; set; } // Total de créditos adquiridos
        public int Utilizados { get; set; } // Créditos já usados
        public DateTime DataCompra { get; set; }
        public DateTime? DataExpiracao { get; set; } // Opcional, caso queira expirar créditos

        public Usuario Usuario { get; set; }
    }
}