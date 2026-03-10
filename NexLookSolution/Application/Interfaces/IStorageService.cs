using Microsoft.AspNetCore.Http;

namespace Application.Interfaces
{
    public interface IStorageService
    {
        Task<StorageUploadResult> UploadAsync(IFormFile file);
        Task<bool> DeleteAsync(string publicId);
    }

    public class StorageUploadResult
    {
        public bool Success { get; set; }
        public string Url { get; set; }
        public string PublicId { get; set; }
        public string ErrorMessage { get; set; }
    }
}