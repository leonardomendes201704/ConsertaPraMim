using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi.Any;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ConsertaPraMim.API.Swagger;

public sealed class ComprehensiveSwaggerOperationFilter : IOperationFilter
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = true
    };

    private static readonly IReadOnlyDictionary<string, string> StatusCodeGuidance = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["200"] = "Operacao concluida com sucesso e retorno do contrato esperado.",
        ["201"] = "Recurso criado com sucesso.",
        ["202"] = "Requisicao aceita para processamento assincrono.",
        ["204"] = "Operacao concluida sem corpo de resposta.",
        ["400"] = "Requisicao invalida (payload, parametros ou regra de dominio).",
        ["401"] = "Token ausente, expirado ou invalido.",
        ["403"] = "Usuario autenticado sem permissao para este recurso.",
        ["404"] = "Recurso nao encontrado para o contexto informado.",
        ["409"] = "Conflito de estado/regra de negocio para executar a operacao.",
        ["422"] = "Entidade validada, mas semanticamente invalida para a regra aplicada.",
        ["429"] = "Limite de requisicoes excedido (rate limit).",
        ["500"] = "Falha interna inesperada no processamento."
    };

    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(context);

        var apiDescription = context.ApiDescription;
        var endpointContext = ApiEndpointDocumentationCatalog.Resolve(apiDescription);
        var httpMethod = (apiDescription.HttpMethod ?? "GET").ToUpperInvariant();
        var relativePath = NormalizeRelativePath(apiDescription.RelativePath);
        var hasIdentifier = apiDescription.ParameterDescriptions.Any(p => p.Source == BindingSource.Path);

        if (string.IsNullOrWhiteSpace(operation.Summary))
        {
            operation.Summary = BuildSummary(httpMethod, endpointContext.ResourceLabel, hasIdentifier);
        }

        var descriptionBuilder = new StringBuilder();
        descriptionBuilder.AppendLine("### Objetivo de negocio");
        descriptionBuilder.AppendLine(BuildBusinessGoal(httpMethod, endpointContext.ResourceLabel, hasIdentifier));
        descriptionBuilder.AppendLine(endpointContext.BusinessContext);
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Contexto tecnico");
        descriptionBuilder.AppendLine("- " + endpointContext.TechnicalContext);
        descriptionBuilder.AppendLine("- Dominio: **" + endpointContext.DomainTitle + "**.");
        descriptionBuilder.AppendLine("- Publico principal: **" + endpointContext.Audience + "**.");
        descriptionBuilder.AppendLine("- Contrato publicado via OpenAPI (`/swagger/v1/swagger.json`).");
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Autenticacao e autorizacao");
        var requiresAuth = AppendAuthSection(descriptionBuilder, apiDescription);
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Parametros de entrada");
        var parameterContext = BuildParameterContext(apiDescription);
        AppendInputParametersSection(descriptionBuilder, parameterContext);
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Respostas e erros esperados");
        AppendResponsesSection(descriptionBuilder, operation);
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Observabilidade e rastreabilidade");
        descriptionBuilder.AppendLine("- Correlacionar chamadas pelo `correlationId` retornado nos headers/logs da API.");
        descriptionBuilder.AppendLine("- Para incidentes, registrar metodo e rota: `" + httpMethod + " " + relativePath + "`.");
        descriptionBuilder.AppendLine("- Validar eventos relacionados no monitoramento administrativo quando aplicavel.");
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Regras e boas praticas");
        foreach (var rule in endpointContext.Rules)
        {
            descriptionBuilder.AppendLine("- " + rule);
        }

        descriptionBuilder.AppendLine("- Tratar explicitamente codigos `401`, `403`, `409` e `429` no consumidor.");
        descriptionBuilder.AppendLine("- Em fluxos sensiveis, aplicar retry apenas quando a operacao for idempotente.");
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Exemplo de chamada (cURL)");
        descriptionBuilder.AppendLine("```bash");
        descriptionBuilder.AppendLine(BuildCurlExample(httpMethod, relativePath, parameterContext, requiresAuth, operation, context.SchemaRepository));
        descriptionBuilder.AppendLine("```");

        var existingDescription = operation.Description?.Trim();
        if (!string.IsNullOrWhiteSpace(existingDescription))
        {
            descriptionBuilder.AppendLine();
            descriptionBuilder.AppendLine("### Notas complementares");
            descriptionBuilder.AppendLine(existingDescription);
        }

        operation.Description = descriptionBuilder.ToString().Trim();
    }

    private static bool AppendAuthSection(StringBuilder builder, ApiDescription apiDescription)
    {
        var endpointMetadata = apiDescription.ActionDescriptor.EndpointMetadata;
        var allowAnonymous = endpointMetadata.OfType<IAllowAnonymous>().Any();
        var authorizePolicies = endpointMetadata
            .OfType<IAuthorizeData>()
            .Where(x => !string.IsNullOrWhiteSpace(x.Policy) || !string.IsNullOrWhiteSpace(x.Roles))
            .ToArray();
        var requiresAuth = endpointMetadata.OfType<IAuthorizeData>().Any() && !allowAnonymous;

        if (!requiresAuth)
        {
            builder.AppendLine("- Endpoint publico (`AllowAnonymous`).");
            builder.AppendLine("- Nao requer header `Authorization`.");
            return false;
        }

        builder.AppendLine("- Requer JWT Bearer no header `Authorization`.");
        builder.AppendLine("- Formato: `Authorization: Bearer {token}`.");

        if (authorizePolicies.Length == 0)
        {
            builder.AppendLine("- Sem policy explicita no endpoint (segue regras gerais de autenticacao).");
            return true;
        }

        foreach (var policy in authorizePolicies)
        {
            if (!string.IsNullOrWhiteSpace(policy.Policy))
            {
                builder.AppendLine($"- Policy: `{policy.Policy}`.");
            }

            if (!string.IsNullOrWhiteSpace(policy.Roles))
            {
                builder.AppendLine($"- Roles permitidas: `{policy.Roles}`.");
            }
        }

        return true;
    }

    private static ParameterContext BuildParameterContext(ApiDescription apiDescription)
    {
        var pathParameters = apiDescription.ParameterDescriptions
            .Where(p => p.Source == BindingSource.Path)
            .ToArray();
        var queryParameters = apiDescription.ParameterDescriptions
            .Where(p => p.Source == BindingSource.Query)
            .ToArray();
        var headerParameters = apiDescription.ParameterDescriptions
            .Where(p => p.Source == BindingSource.Header)
            .ToArray();
        var bodyParameters = apiDescription.ParameterDescriptions
            .Where(p => p.Source == BindingSource.Body)
            .ToArray();

        return new ParameterContext(pathParameters, queryParameters, headerParameters, bodyParameters);
    }

    private static void AppendInputParametersSection(StringBuilder builder, ParameterContext parameterContext)
    {
        if (parameterContext.IsEmpty)
        {
            builder.AppendLine("- Endpoint sem parametros de entrada.");
            return;
        }

        if (parameterContext.Path.Length > 0)
        {
            builder.AppendLine("- **Path**:");
            foreach (var parameter in parameterContext.Path)
            {
                builder.AppendLine($"  - `{parameter.Name}` ({ResolveType(parameter.Type)}): identificador no caminho da rota.");
            }
        }

        if (parameterContext.Query.Length > 0)
        {
            builder.AppendLine("- **Query**:");
            foreach (var parameter in parameterContext.Query)
            {
                builder.AppendLine($"  - `{parameter.Name}` ({ResolveType(parameter.Type)}): filtro/controle de consulta.");
            }
        }

        if (parameterContext.Header.Length > 0)
        {
            builder.AppendLine("- **Header**:");
            foreach (var parameter in parameterContext.Header)
            {
                builder.AppendLine($"  - `{parameter.Name}` ({ResolveType(parameter.Type)}): metadado tecnico da requisicao.");
            }
        }

        if (parameterContext.Body.Length > 0)
        {
            builder.AppendLine("- **Body**:");
            foreach (var parameter in parameterContext.Body)
            {
                builder.AppendLine($"  - `{parameter.Name}` ({ResolveType(parameter.Type)}): payload principal da operacao.");
            }
        }
    }

    private static void AppendResponsesSection(StringBuilder builder, OpenApiOperation operation)
    {
        if (operation.Responses.Count == 0)
        {
            builder.AppendLine("- Sem codigos de resposta documentados explicitamente.");
            return;
        }

        foreach (var response in operation.Responses.OrderBy(x => x.Key, StringComparer.Ordinal))
        {
            var mappedGuidance = StatusCodeGuidance.TryGetValue(response.Key, out var guidance)
                ? guidance
                : "Resposta conforme contrato da operacao.";

            var description = string.IsNullOrWhiteSpace(response.Value.Description)
                ? mappedGuidance
                : response.Value.Description;

            builder.AppendLine($"- **{response.Key}**: {description}");
        }
    }

    private static string BuildCurlExample(
        string httpMethod,
        string relativePath,
        ParameterContext parameterContext,
        bool requiresAuth,
        OpenApiOperation operation,
        SchemaRepository schemaRepository)
    {
        var path = ApplyPathExamples(relativePath, parameterContext.Path);
        var queryPart = BuildQueryString(parameterContext.Query);
        var url = "{{API_BASE_URL}}" + path + queryPart;

        var lines = new List<string>
        {
            $"curl -X {httpMethod} \"{url}\""
        };

        if (requiresAuth)
        {
            lines.Add("  -H \"Authorization: Bearer {token}\"");
        }

        var bodyExample = BuildRequestBodyExample(operation, schemaRepository);
        if (!string.IsNullOrWhiteSpace(bodyExample))
        {
            lines.Add("  -H \"Content-Type: application/json\"");
            lines.Add($"  -d '{bodyExample}'");
        }

        return string.Join(" \\\n", lines);
    }

    private static string ApplyPathExamples(string relativePath, IReadOnlyCollection<ApiParameterDescription> pathParameters)
    {
        if (pathParameters.Count == 0)
        {
            return relativePath;
        }

        var result = relativePath;
        foreach (var parameter in pathParameters)
        {
            var sample = GetSampleStringValue(parameter.Type, parameter.Name);
            result = ReplaceRouteToken(result, parameter.Name, sample);
        }

        return result;
    }

    private static string ReplaceRouteToken(string path, string parameterName, string replacement)
    {
        var exactToken = "{" + parameterName + "}";
        if (path.Contains(exactToken, StringComparison.OrdinalIgnoreCase))
        {
            return path.Replace(exactToken, replacement, StringComparison.OrdinalIgnoreCase);
        }

        return Regex.Replace(
            path,
            "\\{" + Regex.Escape(parameterName) + ":[^}]+\\}",
            replacement,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }

    private static string BuildQueryString(IReadOnlyCollection<ApiParameterDescription> queryParameters)
    {
        if (queryParameters.Count == 0)
        {
            return string.Empty;
        }

        var parts = queryParameters
            .Select(parameter => $"{parameter.Name}={Uri.EscapeDataString(GetSampleStringValue(parameter.Type, parameter.Name))}")
            .ToArray();

        return "?" + string.Join("&", parts);
    }

    private static string BuildRequestBodyExample(OpenApiOperation operation, SchemaRepository schemaRepository)
    {
        if (operation.RequestBody == null)
        {
            return string.Empty;
        }

        if (!operation.RequestBody.Content.TryGetValue("application/json", out var mediaType) || mediaType.Schema == null)
        {
            return string.Empty;
        }

        var sample = BuildSampleObject(mediaType.Schema, schemaRepository, depth: 0, visitedReferences: new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        return sample == null
            ? string.Empty
            : JsonSerializer.Serialize(sample, JsonSerializerOptions).Replace(Environment.NewLine, " ", StringComparison.Ordinal);
    }

    private static object? BuildSampleObject(
        OpenApiSchema schema,
        SchemaRepository schemaRepository,
        int depth,
        HashSet<string> visitedReferences)
    {
        if (depth > 4)
        {
            return "...";
        }

        var resolvedSchema = ResolveSchema(schema, schemaRepository, visitedReferences);
        if (resolvedSchema == null)
        {
            return null;
        }

        if (resolvedSchema.Enum.Count > 0)
        {
            return ConvertOpenApiAnyValue(resolvedSchema.Enum[0]);
        }

        if (resolvedSchema.Type == "object" || resolvedSchema.Properties.Count > 0)
        {
            var dictionary = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in resolvedSchema.Properties)
            {
                dictionary[property.Key] = BuildSampleObject(property.Value, schemaRepository, depth + 1, visitedReferences);
            }

            return dictionary;
        }

        if (resolvedSchema.Type == "array" && resolvedSchema.Items != null)
        {
            return new List<object?> { BuildSampleObject(resolvedSchema.Items, schemaRepository, depth + 1, visitedReferences) };
        }

        return BuildScalarSample(resolvedSchema.Type, resolvedSchema.Format);
    }

    private static OpenApiSchema? ResolveSchema(OpenApiSchema schema, SchemaRepository schemaRepository, HashSet<string> visitedReferences)
    {
        if (schema.Reference == null || string.IsNullOrWhiteSpace(schema.Reference.Id))
        {
            return schema;
        }

        if (!visitedReferences.Add(schema.Reference.Id))
        {
            return schema;
        }

        if (!schemaRepository.Schemas.TryGetValue(schema.Reference.Id, out var resolved))
        {
            return schema;
        }

        return resolved;
    }

    private static object? ConvertOpenApiAnyValue(IOpenApiAny value)
    {
        return value switch
        {
            OpenApiString openApiString => openApiString.Value,
            OpenApiInteger openApiInteger => openApiInteger.Value,
            OpenApiLong openApiLong => openApiLong.Value,
            OpenApiDouble openApiDouble => openApiDouble.Value,
            OpenApiBoolean openApiBoolean => openApiBoolean.Value,
            OpenApiFloat openApiFloat => openApiFloat.Value,
            OpenApiDate openApiDate => openApiDate.Value.ToString("yyyy-MM-dd"),
            OpenApiDateTime openApiDateTime => openApiDateTime.Value.ToString("O"),
            _ => value.ToString()
        };
    }

    private static object BuildScalarSample(string? type, string? format)
    {
        if (string.Equals(type, "string", StringComparison.OrdinalIgnoreCase))
        {
            if (string.Equals(format, "date-time", StringComparison.OrdinalIgnoreCase))
            {
                return "2026-02-23T12:00:00Z";
            }

            if (string.Equals(format, "date", StringComparison.OrdinalIgnoreCase))
            {
                return "2026-02-23";
            }

            if (string.Equals(format, "uuid", StringComparison.OrdinalIgnoreCase))
            {
                return "11111111-2222-3333-4444-555555555555";
            }

            return "string";
        }

        if (string.Equals(type, "integer", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (string.Equals(type, "number", StringComparison.OrdinalIgnoreCase))
        {
            return 1.0;
        }

        if (string.Equals(type, "boolean", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return "value";
    }

    private static string BuildSummary(string httpMethod, string resourceLabel, bool hasIdentifier)
    {
        return httpMethod switch
        {
            "GET" when hasIdentifier => $"Consultar {resourceLabel}",
            "GET" => $"Listar {resourceLabel}",
            "POST" => $"Executar criacao/acao em {resourceLabel}",
            "PUT" => $"Atualizar {resourceLabel}",
            "PATCH" => $"Atualizar parcialmente {resourceLabel}",
            "DELETE" => $"Remover/inativar {resourceLabel}",
            _ => $"Operacao {httpMethod} em {resourceLabel}"
        };
    }

    private static string BuildBusinessGoal(string httpMethod, string resourceLabel, bool hasIdentifier)
    {
        return httpMethod switch
        {
            "GET" when hasIdentifier => $"Recuperar os dados de {resourceLabel} para exibicao, auditoria ou continuidade de fluxo operacional.",
            "GET" => $"Disponibilizar visao de {resourceLabel} com suporte a consulta operacional e tomada de decisao.",
            "POST" => $"Registrar acao de negocio em {resourceLabel}, aplicando validacoes e regras de consistencia.",
            "PUT" => $"Atualizar o estado completo de {resourceLabel} conforme politicas de governanca da plataforma.",
            "PATCH" => $"Alterar parcialmente {resourceLabel} com menor acoplamento e mantendo trilha auditavel.",
            "DELETE" => $"Remover ou desativar {resourceLabel} respeitando integridade e rastreabilidade.",
            _ => $"Executar operacao de negocio em {resourceLabel} com rastreabilidade no ecossistema ConsertaPraMim."
        };
    }

    private static string NormalizeRelativePath(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "/";
        }

        var sanitized = relativePath.Split('?', StringSplitOptions.RemoveEmptyEntries)[0].Trim('/');
        return "/" + sanitized;
    }

    private static string ResolveType(Type? type)
    {
        if (type == null)
        {
            return "desconhecido";
        }

        var nullableType = Nullable.GetUnderlyingType(type) ?? type;
        if (nullableType.IsEnum)
        {
            return $"enum ({nullableType.Name})";
        }

        if (nullableType == typeof(string))
        {
            return "string";
        }

        if (nullableType == typeof(bool))
        {
            return "boolean";
        }

        if (nullableType == typeof(DateTime) || nullableType == typeof(DateTimeOffset))
        {
            return "datetime";
        }

        if (nullableType == typeof(Guid))
        {
            return "guid";
        }

        if (nullableType.IsPrimitive || nullableType == typeof(decimal))
        {
            return nullableType.Name.ToLowerInvariant();
        }

        return nullableType.Name;
    }

    private static string GetSampleStringValue(Type? type, string parameterName)
    {
        var nullableType = type == null ? null : Nullable.GetUnderlyingType(type) ?? type;
        if (nullableType == typeof(Guid))
        {
            return "11111111-2222-3333-4444-555555555555";
        }

        if (nullableType == typeof(DateTime) || nullableType == typeof(DateTimeOffset))
        {
            return "2026-02-23T12:00:00Z";
        }

        if (nullableType == typeof(bool))
        {
            return "true";
        }

        if (nullableType == typeof(int) || nullableType == typeof(long) || nullableType == typeof(short))
        {
            return "1";
        }

        if (!string.IsNullOrWhiteSpace(parameterName) &&
            (parameterName.Contains("email", StringComparison.OrdinalIgnoreCase)))
        {
            return "usuario@exemplo.com";
        }

        if (!string.IsNullOrWhiteSpace(parameterName) &&
            (parameterName.Contains("cep", StringComparison.OrdinalIgnoreCase) ||
             parameterName.Contains("zip", StringComparison.OrdinalIgnoreCase)))
        {
            return "01001000";
        }

        if (!string.IsNullOrWhiteSpace(parameterName) &&
            parameterName.Contains("status", StringComparison.OrdinalIgnoreCase))
        {
            return "active";
        }

        return "valor";
    }

    private sealed record ParameterContext(
        ApiParameterDescription[] Path,
        ApiParameterDescription[] Query,
        ApiParameterDescription[] Header,
        ApiParameterDescription[] Body)
    {
        public bool IsEmpty => Path.Length == 0 && Query.Length == 0 && Header.Length == 0 && Body.Length == 0;
    }
}
