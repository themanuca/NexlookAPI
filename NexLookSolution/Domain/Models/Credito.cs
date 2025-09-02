using System;

namespace Domain.Models
{
    public class Credito
    {
        public Guid Id { get; set; }
        public Guid UsuarioId { get; set; }
        public int Quantidade { get; set; } // Total de cr�ditos adquiridos
        public int Utilizados { get; set; } // Cr�ditos j� usados
        public DateTime DataCompra { get; set; }
        public DateTime? DataExpiracao { get; set; } // Opcional, caso queira expirar cr�ditos

        public Usuario Usuario { get; set; }
    }
}