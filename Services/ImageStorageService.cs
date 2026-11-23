using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace TurnoutStore.Services
{
    public interface IImageStorageService
    {
        Task<string> SaveProductImageAsync(IFormFile file);
        void DeleteProductImage(string? imageUrl);
    }

    public class ImageStorageService : IImageStorageService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<ImageStorageService> _logger;

        private const long MaxFileSizeInBytes = 5 * 1024 * 1024; // 5 MB
        private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private static readonly string[] AllowedContentTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };

        public ImageStorageService(IWebHostEnvironment env, ILogger<ImageStorageService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public async Task<string> SaveProductImageAsync(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                throw new ArgumentException("Uploaded file is empty.", nameof(file));
            }

            if (file.Length > MaxFileSizeInBytes)
            {
                throw new InvalidOperationException($"Image file exceeds maximum permitted size of {MaxFileSizeInBytes / (1024 * 1024)} MB.");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(extension))
            {
                throw new InvalidOperationException($"File extension '{extension}' is not supported. Permitted types: JPG, PNG, WEBP, GIF.");
            }

            if (!AllowedContentTypes.Contains(file.ContentType.ToLowerInvariant()))
            {
                throw new InvalidOperationException("Invalid MIME content-type for image.");
            }

            // Verify Magic Bytes
            using (var stream = file.OpenReadStream())
            {
                if (!IsValidImageHeader(stream, extension))
                {
                    throw new InvalidOperationException("Uploaded file contents do not match expected image format headers.");
                }
            }

            var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsFolder = Path.Combine(webRoot, "images", "products");

            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            var uniqueFileName = $"{Guid.NewGuid():N}{extension}";
            var destinationPath = Path.Combine(uploadsFolder, uniqueFileName);

            using (var outputStream = new FileStream(destinationPath, FileMode.Create))
            {
                await file.CopyToAsync(outputStream);
            }

            _logger.LogInformation("Saved product image {FileName} to {DestinationPath}", file.FileName, destinationPath);
            return $"/images/products/{uniqueFileName}";
        }

        public void DeleteProductImage(string? imageUrl)
        {
            if (string.IsNullOrWhiteSpace(imageUrl)) return;

            // Security guard: Ensure path is within /images/products/ and contains no traversal
            var normalized = imageUrl.TrimStart('/').Replace('\\', '/');
            if (!normalized.StartsWith("images/products/", StringComparison.OrdinalIgnoreCase) || normalized.Contains(".."))
            {
                return;
            }

            try
            {
                var webRoot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
                var relativePath = normalized.Replace('/', Path.DirectorySeparatorChar);
                var fullPath = Path.Combine(webRoot, relativePath);

                if (File.Exists(fullPath))
                {
                    File.Delete(fullPath);
                    _logger.LogInformation("Deleted obsolete product image {Path}", fullPath);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to delete image file {ImageUrl}", imageUrl);
            }
        }

        private static bool IsValidImageHeader(Stream stream, string extension)
        {
            var header = new byte[12];
            int read = stream.Read(header, 0, header.Length);
            stream.Position = 0; // Reset stream position

            if (read < 4) return false;

            switch (extension)
            {
                case ".jpg":
                case ".jpeg":
                    // JPEG: FF D8 FF
                    return header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;

                case ".png":
                    // PNG: 89 50 4E 47 0D 0A 1A 0A
                    return read >= 8 &&
                           header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47 &&
                           header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;

                case ".gif":
                    // GIF: 'GIF87a' or 'GIF89a'
                    return read >= 6 &&
                           header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38 &&
                           (header[4] == 0x37 || header[4] == 0x39) && header[5] == 0x61;

                case ".webp":
                    // WEBP: 'RIFF' .... 'WEBP'
                    return read >= 12 &&
                           header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46 &&
                           header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50;

                default:
                    return false;
            }
        }
    }
}
