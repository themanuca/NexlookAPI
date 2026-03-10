using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class Look
    {
        public Guid Id { get; set; }
        public Guid UsuarioId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descricao { get; set; } = string.Empty;
        public DateTime DataCriacao { get; set; }

        public Usuario Usuario { get; set; }
        public ICollection<LookImage> Images { get; set; } = new List<LookImage>();
    }
}
