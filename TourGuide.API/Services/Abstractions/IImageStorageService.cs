using Microsoft.AspNetCore.Http;

namespace TourGuide.API.Services.Abstractions;

public interface IImageStorageService
{
    Task<string> UploadPoiImageAsync(IFormFile file, CancellationToken cancellationToken = default);
}
