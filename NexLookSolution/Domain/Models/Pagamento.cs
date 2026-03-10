using Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Pagamento
    {
        public Guid Id { get; set; }
        public Guid SubscricaoId { get; set; }
        public decimal Valor { get; set; }
        public DateTime DataPagamento { get; set; }
        public string MetodoPagamento { get; set; } = string.Empty;
        public string TransacaoId { get; set; } = string.Empty;
        public StatusPagamentoEnum Status { get; set; } = StatusPagamentoEnum.Pendente;

        public Subscricao Subscricao { get; set; }
    }
}
