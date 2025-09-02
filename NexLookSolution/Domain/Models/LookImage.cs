using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Models
{
    public class LookImage
    {
        public Guid Id { get; set; }
        public Guid LookId { get; set; }
        public string ImageUrl { get; set; } = string.Empty;

        public Look Look { get; set; }
    }
}
