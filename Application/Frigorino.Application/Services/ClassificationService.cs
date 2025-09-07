#pragma warning disable OPENAI001 // Der Typ dient nur zu Testzwecken und kann in zukünftigen Aktualisierungen geändert oder entfernt werden. Unterdrücken Sie diese Diagnose, um fortzufahren.
using Frigorino.Application.DTOs;
using Frigorino.Application.Models;
using Frigorino.Domain.Entities;
using Frigorino.Domain.Interfaces;
using Frigorino.Infrastructure.EntityFramework;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using OpenAI;
using OpenAI.Responses;

namespace Frigorino.Application.Services
{
    public class ClassificationService : IClassificationService
    {
        private readonly OpenAIResponseClient _client;

        private readonly ApplicationDbContext _applicationDbContext;

        public ClassificationService(ApplicationDbContext applicationDbContext,
            IConfiguration configuration)
        {
            _applicationDbContext = applicationDbContext;

            var apiKey = configuration["OpenAiSettings:APIKey"];
            var model = configuration["OpenAiSettings:Model"];

            var openAi = new OpenAIClient(apiKey);
            _client = openAi.GetOpenAIResponseClient(model);
        }

        public async Task Classify(IEnumerable<int> listItemIds)
        {
            // First, try to match existing classifications
            var listItems = _applicationDbContext.ListItems.Where(l => l.IsActive && l.Classification == null).Where(i => listItemIds.Contains(i.Id)).ToList();
            foreach (var item in listItems)
            {
                var existing = await _applicationDbContext.ArticleClassifications.FirstOrDefaultAsync(c => c.OriginalName.Equals(item.Text));
                if (existing != null)
                {
                    item.Classification = existing;
                }
            }
            await _applicationDbContext.SaveChangesAsync();

            // Then classify the remaining items using OpenAI
            listItems = _applicationDbContext.ListItems.Where(l => l.IsActive && l.Classification == null).Where(i => listItemIds.Contains(i.Id)).ToList();
            if (listItems.Count == 0)
            {
                return;
            }

            try
            {
                string input = JsonConvert.SerializeObject(listItems.Select(l => l.Text).ToArray());
                var response = await _client.CreateResponseAsync([
                    ResponseItem.CreateDeveloperMessageItem(ChatRequest.CreateArticleClassificationRequest().PromptContent),
                    ResponseItem.CreateUserMessageItem(input),
                ]);

                var responseContent = response.Value.GetOutputText();
                if (!string.IsNullOrEmpty(responseContent))
                {
                    var result = JsonConvert.DeserializeObject<ArticleClassificationResponse[]>(responseContent);

                    for (int i = 0; i < Math.Min(result!.Length, listItems.Count); i++)
                    {
                        var classificationResponse = result[i];
                        var listItem = listItems[i];

                        ArticleClassification classification = null!;

                        // Error classifications
                        if (!string.IsNullOrEmpty(classificationResponse.Error))
                        {
                            classification = new ArticleClassification()
                            {
                                OriginalName = listItem.Text,
                                Category = ClassificationCategory.Error,
                                HintCategory = classificationResponse.Error,
                            };
                        }
                        else
                        {
                            // Parse the category string to enum
                            if (!Enum.TryParse<ClassificationCategory>(classificationResponse.Category, true, out var category))
                            {
                                category = ClassificationCategory.Error;
                            }

                            classification = new ArticleClassification()
                            {
                                OriginalName = listItem.Text,
                                Category = category,
                                ExpirationDuration = (classificationResponse.MinExpirationDuration + classificationResponse.MaxExpirationDuration) / 2,
                                HintCategory = classificationResponse.Hint.HintCategory ?? string.Empty,
                                HintEstimation = classificationResponse.Hint.HintEstimation
                            };
                        }

                        // Add to database and assign to list item
                        _applicationDbContext.ArticleClassifications.Add(classification);
                        listItem.Classification = classification;
                    }

                    await _applicationDbContext.SaveChangesAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                throw;
            }
        }
    }
}
#pragma warning restore OPENAI001 // Der Typ dient nur zu Testzwecken und kann in zukünftigen Aktualisierungen geändert oder entfernt werden. Unterdrücken Sie diese Diagnose, um fortzufahren.