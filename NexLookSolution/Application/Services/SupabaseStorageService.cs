using Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace Application.Services
{
    public class SupabaseStorageService : IStorageService
    {
        private readonly HttpClient _httpClient;
        private readonly string _supabaseUrl;
        private readonly string _serviceKey;
        private readonly string _bucket;
        private readonly ILogger<SupabaseStorageService> _logger;

        public SupabaseStorageService(
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            ILogger<SupabaseStorageService> logger)
        {
            _httpClient = httpClientFactory.CreateClient();
            _supabaseUrl = configuration["Storage:SUPABASE_URL"]!.TrimEnd('/');
            _serviceKey = configuration["Storage:SUPABASE_SERVICE_ROLE_KEY"]!;

            _bucket = configuration["Storage:SUPABASE_BUCKET"] ?? "looks";
            _logger = logger;
        }

        public async Task<StorageUploadResult> UploadAsync(IFormFile file)
        {
            try
            {
                using var compressedStream = new MemoryStream();
                using (var image = await Image.LoadAsync(file.OpenReadStream()))
                {
                    if (image.Width > 1200 || image.Height > 1200)
                    {
                        image.Mutate(x => x.Resize(new ResizeOptions
                        {
                            Mode = ResizeMode.Max,
                            Size = new SixLabors.ImageSharp.Size(1200, 1200)
                        }));
                    }
                    await image.SaveAsJpegAsync(compressedStream, new JpegEncoder { Quality = 80 });
                }

                compressedStream.Position = 0;

                var filePath = $"{Guid.NewGuid()}.jpg";
                var uploadUrl = $"{_supabaseUrl}/storage/v1/object/{_bucket}/{filePath}";

                using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
                request.Headers.Add("Authorization", $"Bearer {_serviceKey}");
                request.Content = new StreamContent(compressedStream);
                request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/jpeg");

                var response = await _httpClient.SendAsync(request);
                var body = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError("Erro no upload para Supabase. Status: {Status}, Body: {Body}", response.StatusCode, body);
                    return new StorageUploadResult { Success = false, ErrorMessage = body };
                }

                var publicUrl = $"{_supabaseUrl}/storage/v1/object/public/{_bucket}/{filePath}";

                return new StorageUploadResult
                {
                    Success = true,
                    Url = publicUrl,
                    PublicId = filePath
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao fazer upload para o Supabase");
                return new StorageUploadResult { Success = false, ErrorMessage = ex.Message };
            }
        }

        public async Task<bool> DeleteAsync(string publicId)
        {
            try
            {
                var deleteUrl = $"{_supabaseUrl}/storage/v1/object/{_bucket}";

                using var request = new HttpRequestMessage(HttpMethod.Delete, deleteUrl);
                request.Headers.Add("Authorization", $"Bearer {_serviceKey}");

                var payload = System.Text.Json.JsonSerializer.Serialize(new { prefixes = new[] { publicId } });
                request.Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    _logger.LogError("Erro ao deletar do Supabase. Status: {Status}, Body: {Body}", response.StatusCode, body);
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao deletar arquivo do Supabase. PublicId: {PublicId}", publicId);
                return false;
            }
        }
    }
}
