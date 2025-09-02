using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Plano
    {
        public Guid Id { get; set; }
        public string Nome { get; set; } = string.Empty;
        public decimal Preco { get; set; }
        public int DuracaoEmDias { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public bool IsAtivo { get; set; }

        public ICollection<Subscricao> Subscriptions { get; set; } = new List<Subscricao>();
    }
}
