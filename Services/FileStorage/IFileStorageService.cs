using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace StarApi.Services.Interfaces
{
    public interface IFileStorageService
    {
        Task<string> SaveImageAsync(IFormFile imageFile, string folderName, string? fileName = null);
        Task<bool> DeleteImageAsync(string filePath);
        Task<string> GetFileUrlAsync(string filePath);
        bool IsValidImage(IFormFile imageFile);
        (int width, int height) GetImageDimensions(IFormFile imageFile);
        Task<string> OptimizeImageAsync(IFormFile imageFile, int maxWidth = 800);
        Task<(byte[] data, string mimeType)> GetUserAvatarAsync(Guid userId);
    }
}