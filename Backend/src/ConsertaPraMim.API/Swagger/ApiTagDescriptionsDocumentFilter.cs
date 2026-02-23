using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ConsertaPraMim.API.Swagger;

public sealed class ApiTagDescriptionsDocumentFilter : IDocumentFilter
{
    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(swaggerDoc);

        var tagNames = swaggerDoc.Paths
            .SelectMany(path => path.Value.Operations.Values)
            .SelectMany(operation => operation.Tags ?? [])
            .Select(tag => tag.Name)
            .Where(tagName => !string.IsNullOrWhiteSpace(tagName))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tagName => tagName, StringComparer.OrdinalIgnoreCase)
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
