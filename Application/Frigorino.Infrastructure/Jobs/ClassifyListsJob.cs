using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.Extensions.Logging;

namespace Frigorino.Infrastructure.Jobs
{
    public class ClassifyListsJob
    {
        private readonly ILogger<ClassifyListsJob> _logger;
        private readonly ApplicationDbContext _dbContext;
        private readonly IListService _listService;

        public ClassifyListsJob(ILogger<ClassifyListsJob> logger, ApplicationDbContext dbContext, IListService listService)
        {
            _logger = logger;
            _dbContext = dbContext;
            _listService = listService;
        }

        public async Task ExecuteAsync()
        {
            var activeLists = _dbContext.Lists.Where(l => l.IsActive).Select(l => l.Id).ToList();
            foreach (var list in activeLists)
            {
                await _listService.ClassifyItems(list, "0", true);
            }
        }
    }
}
