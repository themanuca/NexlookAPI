using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.DTOs
{
    public class RoupaItem
    {
        public IFormFile File { get; set; }
        public string? Categoria { get; set; }
        public string? Nome { get; set; }
        public string Id { get; set; } = Guid.NewGuid().ToString();
    }
}
