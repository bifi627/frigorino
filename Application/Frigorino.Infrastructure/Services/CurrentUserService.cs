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
                var id = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? "0";

                return id;
            }
        }

        public string? Email
        {
            get
            {
                // Firebase JWT uses "email" claim for the user email
                var email = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.Email);
                return email;
            }
        }

        public string? UserName
        {
            get
            {
                // Firebase JWT uses "name" claim for the user name
                var name = _httpContextAccessor.HttpContext?.User?.FindFirstValue("name");
                return name;
            }
        }
    }
}
