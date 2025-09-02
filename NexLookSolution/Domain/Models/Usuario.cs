using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Usuario
    {
        public Guid Id { get; set; }
        public required string Nome { get; set; }
        public required string Email { get; set; }
        public required string SenhaHash { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime DataAtualizacao { get; set; }
        public ICollection<Subscricao> Subscriptions { get; set; } = new List<Subscricao>();
        public ICollection<Look> Looks { get; set; } = new List<Look>();
    }
}
