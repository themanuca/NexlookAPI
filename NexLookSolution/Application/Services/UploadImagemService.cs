using Application.DTOs;
using Application.Interfaces;
using Domain.Models;
using Infra.dbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Application.Services
{
    public class UploadImagemService : IUploadImagemService
    {
        private readonly AppDbContext _context;
        private readonly IStorageService _storageService;

        public UploadImagemService(IConfiguration configuration, AppDbContext context, IStorageService storage)
        {
            _context = context;
            _storageService = storage;
        }

        public async Task<UploadResponse> UploadLookAsync(RoupaItem roupa, Guid usuarioId)
        {
            if (roupa == null || roupa.File == null || roupa.File.Length == 0)
            {
                return new UploadResponse { Sucesso = false, Mensagem = "Nenhuma imagem fornecida." };
            }

            var looksCount = await _context.Looks.CountAsync(l => l.UsuarioId == usuarioId);
            if (looksCount >= 15)
            {
                return new UploadResponse
                {
                    Sucesso = false,
                    Mensagem = "Número máximo de looks atingido. Exclua alguns looks para adicionar novos."
                };
            }

            try
            {
                var uploadResult = await _storageService.UploadAsync(roupa.File);
                if (!uploadResult.Success)
                {
                    return new UploadResponse { Sucesso = false, Mensagem = uploadResult.ErrorMessage };
                }

                if (roupa.Categoria == null || roupa.Nome == null)
                {
                    return new UploadResponse { Sucesso = false, Mensagem = "Categoria ou Nome não fornecidos." };
                }

                var usuario = await _context.Usuarios.FindAsync(usuarioId);
                if (usuario == null)
                {
                    return new UploadResponse { Sucesso = false, Mensagem = "Usuário não encontrado." };
                }

                var look = new Look
                {
                    Id = Guid.NewGuid(),
                    UsuarioId = usuario.Id,
                    Titulo = roupa.Nome,
                    Descricao = roupa.Categoria,
                    DataCriacao = DateTime.UtcNow,
                    Usuario = usuario
                };

                var lookImage = new LookImage
                {
                    Id = Guid.NewGuid(),
                    LookId = look.Id,
                    ImageUrl = uploadResult.Url,
                    PublicIdCloudnary = uploadResult.PublicId,
                    Look = look
                };

                await _context.Looks.AddAsync(look);
                await _context.LookImages.AddAsync(lookImage);
                await _context.SaveChangesAsync();

                return new UploadResponse
                {
                    Sucesso = true,
                    ImageUrl = uploadResult.Url,
                    Id = look.Id,
                    Mensagem = "Look criado com sucesso!"
                };
            }
            catch (Exception ex)
            {
                return new UploadResponse { Sucesso = false, Mensagem = $"Erro durante o upload: {ex.Message}" };
            }
        }

        public async Task<UploadResponse> UploadImagemAsync(RoupaItem roupaItem)
        {
            if (roupaItem == null || roupaItem.File == null || roupaItem.File.Length == 0)
            {
                return new UploadResponse { Sucesso = false, Mensagem = "Nenhuma imagem fornecida." };
            }

            try
            {
                var uploadResult = await _storageService.UploadAsync(roupaItem.File);
                if (!uploadResult.Success)
                {
                    return new UploadResponse { Sucesso = false, Mensagem = uploadResult.ErrorMessage };
                }

                return new UploadResponse
                {
                    Sucesso = true,
                    ImageUrl = uploadResult.Url,
                    Id = Guid.NewGuid(),
                    PublicIdCloudnary = uploadResult.PublicId,
                    Mensagem = "Upload realizado com sucesso!"
                };
            }
            catch (Exception ex)
            {
                return new UploadResponse { Sucesso = false, Mensagem = $"Erro durante o upload: {ex.Message}" };
            }
        }

        public async Task<List<LookDTO>> BuscarLooksUsuarioAsync(Guid usuarioId)
        {
            var looks = await _context.Looks
                .Include(l => l.Images)
                .Where(l => l.UsuarioId == usuarioId)
                .Select(l => new LookDTO
                {
                    Id = l.Id,
                    Titulo = l.Titulo,
                    Descricao = l.Descricao,
                    DataCriacao = l.DataCriacao,
                    Images = l.Images.Select(i => new LookImageDTO
                    {
                        Id = i.Id,
                        ImageUrl = i.ImageUrl
                    }).ToList()
                })
                .ToListAsync();
            return looks;
        }

        public async Task<DeleteResponse> ExcluirLookAsync(Guid lookId, Guid usuarioId)
        {
            var look = await _context.Looks
                .Include(l => l.Images)
                .FirstOrDefaultAsync(l => l.Id == lookId && l.UsuarioId == usuarioId);

            if (look == null)
            {
                return new DeleteResponse { Sucesso = false, Mensagem = "Look não encontrado ou não pertence ao usuário." };
            }

            foreach (var image in look.Images)
            {
                if (!string.IsNullOrEmpty(image.PublicIdCloudnary))
                {
                    var deleted = await _storageService.DeleteAsync(image.PublicIdCloudnary);
                    if (!deleted)
                    {
                        return new DeleteResponse { Sucesso = false, Mensagem = "Falha ao excluir imagem do storage." };
                    }
                }
            }

            _context.LookImages.RemoveRange(look.Images);
            _context.Looks.Remove(look);
            await _context.SaveChangesAsync();

            return new DeleteResponse { Sucesso = true, Mensagem = "Look e imagens excluídos com sucesso." };
        }
    }
}
