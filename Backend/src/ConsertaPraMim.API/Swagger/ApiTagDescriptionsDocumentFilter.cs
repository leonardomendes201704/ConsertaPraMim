using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ConsertaPraMim.API.Swagger;

public sealed class ApiTagDescriptionsDocumentFilter : IDocumentFilter
{
    private static readonly IReadOnlyDictionary<string, int> PreferredTagOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
    {
        ["Auth"] = 0,
        ["TelegramChatbot"] = 1,
        ["Chats"] = 2,
        ["ServiceRequests"] = 3,
        ["ServiceAppointments"] = 4
    };

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(swaggerDoc);

        var tagNames = swaggerDoc.Paths
            .SelectMany(path => path.Value.Operations.Values)
            .SelectMany(operation => operation.Tags ?? [])
            .Select(tag => tag.Name)
            .Where(tagName => !string.IsNullOrWhiteSpace(tagName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tagName => PreferredTagOrder.TryGetValue(tagName, out var order) ? order : int.MaxValue)
            .ThenBy(tagName => tagName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (tagNames.Length == 0)
        {
            return;
        }

        swaggerDoc.Tags = tagNames
            .Select(tagName =>
            {
                var context = ApiEndpointDocumentationCatalog.ResolveTag(tagName);
                return new OpenApiTag
                {
                    Name = context.TagName,
                    Description = context.Description
                };
            })
            .ToList();
    }
}
