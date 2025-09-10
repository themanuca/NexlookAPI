using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Application.DTOs.IAdto.LookPromptDTO;

namespace Application.Interfaces
{
    public interface IAIService
    {
        Task<string> GerarDescricaoImagemAsync(Guid usuarioId, string prompt);
        Task<LookResponse?> GerarDescricaoImagemcomFOTOAsync(Guid usuarioId, string promptUsuario);
    }
}
