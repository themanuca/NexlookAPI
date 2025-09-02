using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Subscricao
    {
        public Guid Id { get; set; }
        public Guid UsuarioId { get; set; }
        public Guid PlanoId { get; set; }
        public DateTime DataInicial { get; set; }
        public DateTime DataFim { get; set; }
        public bool IsAtivo { get; set; }
        public StatusPagamentoEnum StatusPagamento { get; set; } = StatusPagamentoEnum.Pendente;

        public Usuario Usuario { get; set; }
        public Plano Plano { get; set; }
        public ICollection<Pagamento> Payments { get; set; } = new List<Pagamento>();
    }
}
