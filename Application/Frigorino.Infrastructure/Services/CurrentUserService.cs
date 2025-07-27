using Frigorino.Domain.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Frigorino.Infrastructure.Services
{
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        // Dont inject DbContexts here!!!! Migrations will hang...
        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public string UserId
        {
            get
            {
                // Firebase JWT uses "user_id" claim for the user identifier
                var id = _httpContextAccessor.HttpContext?.User?.FindFirstValue("user_id") 
                    ?? _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier) 
                    ?? "0";

                return id;
            }
        }
    }
}
