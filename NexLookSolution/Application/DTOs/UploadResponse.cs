using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs
{
    public class UploadResponse
    {
        public string ImageUrl { get; set; }
        public Guid Id { get; set; }
        public bool Sucesso { get; set; }
        public string Mensagem { get; set; }
}
}
