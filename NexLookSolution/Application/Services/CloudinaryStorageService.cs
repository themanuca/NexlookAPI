using Application.DTOs;
using Application.Interfaces;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Application.Services
{
    public class CloudinaryStorageService : IStorageService
    {
        private readonly Cloudinary _cloudinary;
        private readonly string _uploadPreset;
        private readonly ILogger<CloudinaryStorageService> _logger;

        public CloudinaryStorageService(
            IConfiguration configuration,
            ILogger<CloudinaryStorageService> logger)
        {
            var account = new Account(
                configuration["Storage:CLOUDINARY_CLOUD_NAME"],
                configuration["Storage:CLOUDINARY_API_KEY"],
                configuration["Storage:CLOUDINARY_API_SECRET"]
            );
            _cloudinary = new Cloudinary(account);
            _uploadPreset = configuration["Storage:CLOUDINARY_UPLOAD_PRESET"];
            _logger = logger;
        }
        public async Task<bool> DeleteAsync(string publicId)
        {
            try
            {
                var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId));
                return result.Error == null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao excluir arquivo do Cloudinary");
                return false;
            }
        }

        public Task<bool> DeleteFirebaseAsync(string publicId)
        {
            throw new NotImplementedException();
        }

        public async Task<StorageUploadResult> UploadAsync(IFormFile file)
        {
            try
            {
                using var compressedStream = new MemoryStream();
                using (var image = await Image.LoadAsync(file.OpenReadStream()))
                {
                    // Redimensionar se maior que 1200x1200
                    if (image.Width > 1200 || image.Height > 1200)
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Max,
                            Size = new SixLabors.ImageSharp.Size(1200, 1200)
                        }));
                    }

                    // Configurar qualidade JPEG
                    var encoder = new JpegEncoder
                    {
                        Quality = 80 // Ajuste conforme necessário (0-100)
                    };

                    await image.SaveAsJpegAsync(compressedStream, encoder);
                }

                compressedStream.Position = 0;
                var uploadParams = new ImageUploadParams
                {
                    File = new FileDescription(file.FileName, compressedStream),
                    UploadPreset = _uploadPreset
                };

                var result = await _cloudinary.UploadAsync(uploadParams);

                if (result.Error != null)
                {
                    return new StorageUploadResult
                    {
                        Success = false,
                        ErrorMessage = result.Error.Message
                    };
                }

                return new StorageUploadResult
                {
                    Success = true,
                    Url = result.SecureUrl.ToString(),
                    PublicId = result.PublicId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao fazer upload para o Cloudinary");
                return new StorageUploadResult
                {
                    Success = false,
                    ErrorMessage = ex.Message
                };
            }
        }

        public Task<StorageUploadResult> UploadFirebaseAsync(IFormFile file)
        {
            throw new NotImplementedException();
        }
    }
}
