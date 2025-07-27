using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;

namespace Frigorino.Web.Middlewares
{
    public sealed class InitialConnectionMiddleware : IMiddleware
    {
        private static readonly HashSet<string> _checkedConnections = [];
        private readonly ICurrentUserService _currentUserService;
        private readonly ApplicationDbContext _applicationDbContext;

        public InitialConnectionMiddleware(ICurrentUserService currentUserService, ApplicationDbContext applicationDbContext)
        {
            _currentUserService = currentUserService;
            _applicationDbContext = applicationDbContext;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (_currentUserService.UserId != "0" && !_checkedConnections.Contains(_currentUserService.UserId))
            {
                if (!await _applicationDbContext.Users.AnyAsync(user => user.ExternalId == _currentUserService.UserId))
                {
                    _applicationDbContext.Users.Add(new Domain.Entities.User
                    {
                        ExternalId = _currentUserService.UserId,
                        Name = $"User_{Guid.NewGuid()}",
                    });
                    await _applicationDbContext.SaveChangesAsync();
                }

                _checkedConnections.Add(_currentUserService.UserId);
            }

            await next(context);
        }
    }
}