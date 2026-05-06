using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Options;
using TourGuide.API.Infrastructure.Options;
using TourGuide.API.Services.Abstractions;

namespace TourGuide.API.Services.Implementations;

public sealed class CloudinaryImageStorageService : IImageStorageService
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif",
    };

    private readonly CloudinaryOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CloudinaryImageStorageService(
        IOptions<CloudinaryOptions> options,
        IWebHostEnvironment environment,
        IHttpContextAccessor httpContextAccessor)
    {
        _options = options.Value;
        _environment = environment;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<string> UploadPoiImageAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
        {
            throw new ArgumentException("Missing image file.");
        }

        ValidateImage(file);

        if (!IsCloudinaryConfigured())
        {
            return await SaveLocalAsync(file, cancellationToken);
        }

        var account = new Account(_options.CloudName, _options.ApiKey, _options.ApiSecret);
        var cloudinary = new Cloudinary(account);

        await using var stream = file.OpenReadStream();
        var uploadResult = await cloudinary.UploadAsync(new ImageUploadParams
        {
            File = new FileDescription(file.FileName, stream),
            Folder = "TourGuide",
        }, cancellationToken);

        if (uploadResult.Error != null)
        {
            throw new InvalidOperationException(uploadResult.Error.Message);
        }

        if (uploadResult.SecureUrl == null)
        {
            throw new InvalidOperationException("Cloudinary did not return an image URL.");
        }

        return uploadResult.SecureUrl.ToString();
    }

    private bool IsCloudinaryConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.CloudName) &&
               !string.IsNullOrWhiteSpace(_options.ApiKey) &&
               !string.IsNullOrWhiteSpace(_options.ApiSecret);
    }

    private static void ValidateImage(IFormFile file)
    {
        if (file.Length > 5 * 1024 * 1024)
        {
            throw new ArgumentException("Image file must be 5MB or smaller.");
        }

        var extension = Path.GetExtension(file.FileName);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            throw new ArgumentException("Unsupported image type. Use jpg, png, webp, or gif.");
        }

        if (!string.IsNullOrWhiteSpace(file.ContentType) &&
            !file.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Uploaded file is not an image.");
        }
    }

    private async Task<string> SaveLocalAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var webRoot = _environment.WebRootPath;
        if (string.IsNullOrWhiteSpace(webRoot))
        {
            webRoot = Path.Combine(_environment.ContentRootPath, "wwwroot");
        }

        var uploadDirectory = Path.Combine(webRoot, "uploads", "poi");
        Directory.CreateDirectory(uploadDirectory);

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var fileName = $"{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadDirectory, fileName);

        await using (var output = File.Create(filePath))
        await using (var input = file.OpenReadStream())
        {
            await input.CopyToAsync(output, cancellationToken);
        }

        var request = _httpContextAccessor.HttpContext?.Request;
        if (request == null)
        {
            return $"/uploads/poi/{fileName}";
        }

        return $"{request.Scheme}://{request.Host}/uploads/poi/{fileName}";
    }
}
