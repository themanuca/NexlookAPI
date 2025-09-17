using Application.DTOs;
using Application.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Domain.Models;
using Infra.dbContext;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Application.Services
{
    public class UploadImagemService : IUploadImagemService
    {
        private readonly Cloudinary _cloudinary;
        private readonly string _uploadPreset;
        private readonly AppDbContext _context;
        private readonly IStorageService _storageService;
        public UploadImagemService(IConfiguration configuration, AppDbContext context, IStorageService storage)
        {
            var account = new Account(
                configuration["Storage:CLOUDINARY_CLOUD_NAME"],
                configuration["Storage:CLOUDINARY_API_KEY"],
                configuration["Storage:CLOUDINARY_API_SECRET"]
            );
            _cloudinary = new Cloudinary(account);
            _uploadPreset = configuration["Storage:CLOUDINARY_UPLOAD_PRESET"];
            _context = context;
            _storageService  = storage;
        }

        public async Task<UploadResponse> UploadLookAsync(RoupaItem roupa, Guid usuarioId)
        {
            if (roupa == null || roupa.File == null || roupa.File.Length == 0)
            {
                return new UploadResponse
                {
                    Sucesso = false,
                    Mensagem = "Nenhuma imagem fornecida."
                };
            }

            var isImagemMax = BuscarLooksUsuarioAsync(usuarioId).Result.Count >= 15;
            if (isImagemMax)
            {
                return new UploadResponse
                {
                    Sucesso = false,
                    Mensagem = "Número máximo de looks atingido. Exclua alguns looks para adicionar novos."
                };
            }
            try
            {
               
                var uploadResult = await UploadImagemAsync(roupa);
                if (!uploadResult.Sucesso)
                {
                    return uploadResult;
                }

                if(roupa.Categoria == null || roupa.Nome == null)
                {
                    return new UploadResponse
                    {
                        Sucesso = false,
                        Mensagem = "Categoria ou Nome não fornecidos."
                    };
                }
                var usuario = await _context.Usuarios.FindAsync(usuarioId);
                if (usuario == null)
                {
                    return new UploadResponse
                    {
                        Sucesso = false,
                        Mensagem = "Usuário não encontrado."
                    };
                }

                // Cria o Look
                var look = new Look
                {
                    Id = uploadResult.Id,
                    UsuarioId = usuario.Id,
                    Titulo = roupa.Nome,
                    Descricao = roupa.Categoria,
                    DataCriacao = DateTime.UtcNow,
                    Usuario = usuario
                };

                // Cria a imagem associada ao Look
                var lookImage = new LookImage
                {
                    LookId = look.Id,
                    Id = Guid.NewGuid(),
                    ImageUrl = uploadResult.ImageUrl,
                    PublicIdCloudnary = uploadResult.PublicIdCloudnary,
                    PublicIdFirebase = uploadResult.PublicIdFirebase,
                    Look = look
                };

                await _context.Looks.AddAsync(look);
                await _context.LookImages.AddAsync(lookImage);
                await _context.SaveChangesAsync();

                return new UploadResponse
                {
                    Sucesso = true,
                    ImageUrl = uploadResult.ImageUrl,
                    Id = look.Id,
                    Mensagem = "Look criado com sucesso!"
                };
            }
            catch (Exception ex)
            {
                return new UploadResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro durante o upload: {ex.Message}"
                };
            }
        }
        public async Task<UploadResponse> UploadImagemAsync(RoupaItem roupaItem)
        {
            if (roupaItem == null || roupaItem.File == null || roupaItem.File.Length == 0)
            {
                return new UploadResponse
                {
                    Sucesso = false,
                    Mensagem = "Nenhuma imagem fornecida."
                };
            }

            try
            {
                using var stream = roupaItem.File.OpenReadStream();
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(roupaItem.File.FileName, stream),
                    UploadPreset = _uploadPreset,
                    // Adicionar transformações para otimizar a imagem
                    Transformation = new Transformation()
                        .Quality("auto") // Qualidade automática
                        .FetchFormat("auto") // Formato automático (WebP quando possível)
                        .Width(1200) // Largura máxima
                        .Height(1200) // Altura máxima
                        .Crop("limit") // Redimensiona mantendo proporção
                        .ColorSpace("srgb")
                };

                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.Error != null)
                {
                    return new UploadResponse
                    {
                        Sucesso = false,
                        Mensagem = $"Erro Cloudinary: {uploadResult.Error.Message}"
                    };
                }

                return new UploadResponse
                {
                    Sucesso = true,
                    ImageUrl = uploadResult.SecureUrl.ToString(),
                    Id = Guid.NewGuid(),
                    PublicIdCloudnary = uploadResult.PublicId,
                    Mensagem = "Upload realizado com sucesso!"
                };
            }
            catch (Exception ex)
            {
                return new UploadResponse
                {
                    Sucesso = false,
                    Mensagem = $"Erro durante o upload: {ex.Message}"
                };
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
                return new DeleteResponse
                {
                    Sucesso = false,
                    Mensagem = "Look não encontrado ou não pertence ao usuário."
                };
            }
            foreach (var image in look.Images)
            {
                if (image?.PublicIdFirebase?.Length > 0)
                {

                }else if(image?.PublicIdCloudnary?.Length > 0)
                {
                    var result = await _storageService.DeleteAsync(image.PublicIdCloudnary);
                    if(!result)
                    {
                        return new DeleteResponse
                        {
                            Sucesso = false,
                            Mensagem = "Falha ao excluir imagem do Cloudinary."
                        };
                    }

                }

            }
            _context.LookImages.RemoveRange(look.Images);
            // Remove o look
            _context.Looks.Remove(look);
            await _context.SaveChangesAsync();
            return new DeleteResponse
            {
                Sucesso = true,
                Mensagem = "Look e imagens excluídos com sucesso."
            };    
        }
    }
}
