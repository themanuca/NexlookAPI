using Application.DTOs;
using Application.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.Extensions.Configuration;

namespace Application.Services
{
    public class UploadImagemService : IUploadImagemService
    {
        private readonly Cloudinary _cloudinary;
        private readonly string _uploadPreset;

        public UploadImagemService(IConfiguration configuration)
        {
            var account = new Account(
                configuration["Storage:CLOUDINARY_CLOUD_NAME"],
                configuration["Storage:CLOUDINARY_API_KEY"],
                configuration["Storage:CLOUDINARY_API_SECRET"]
            );
            _cloudinary = new Cloudinary(account);
            _uploadPreset = configuration["Storage:CLOUDINARY_UPLOAD_PRESET"];
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
                    UploadPreset = _uploadPreset
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
    }
}
