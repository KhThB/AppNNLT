using TourGuide.Domain.Models;

namespace TourGuide.API.Services.Abstractions;

public interface IJwtTokenService
{
    string CreateToken(User user, string sessionId);
}
