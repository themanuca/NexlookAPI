using Application.DTOs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Interfaces
{
    public interface IUploadImagemService
    {
        Task<UploadResponse> UploadImagemAsync(RoupaItem roupaItem);
        Task<UploadResponse> UploadLookAsync(RoupaItem roupa, Guid usuarioId);
    }
}
