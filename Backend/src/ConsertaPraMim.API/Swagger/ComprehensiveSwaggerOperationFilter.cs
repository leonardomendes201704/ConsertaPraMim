using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace ConsertaPraMim.API.Swagger;

public sealed class ComprehensiveSwaggerOperationFilter : IOperationFilter
{
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
        var httpMethod = (apiDescription.HttpMethod ?? "GET").ToUpperInvariant();
        var relativePath = NormalizeRelativePath(apiDescription.RelativePath);
        var hasIdentifier = apiDescription.ParameterDescriptions.Any(p => p.Source == BindingSource.Path);
        var resourceLabel = InferResourceLabel(apiDescription);

        if (string.IsNullOrWhiteSpace(operation.Summary))
        {
            operation.Summary = BuildSummary(httpMethod, resourceLabel, hasIdentifier);
        }

        var descriptionBuilder = new StringBuilder();
        descriptionBuilder.AppendLine("### Objetivo de negocio");
        descriptionBuilder.AppendLine(BuildBusinessGoal(httpMethod, resourceLabel, hasIdentifier));
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Contexto tecnico");
        descriptionBuilder.AppendLine("- Endpoint REST consumido por portais web e/ou aplicativos mobile do ecossistema ConsertaPraMim.");
        descriptionBuilder.AppendLine("- Contrato publicado no Swagger e validado pelo pipeline de API.");
        descriptionBuilder.AppendLine("- Datas devem ser tratadas em UTC no contrato e convertidas para fuso de exibicao no front.");
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Autenticacao e autorizacao");
        AppendAuthSection(descriptionBuilder, apiDescription);
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Parametros de entrada");
        AppendInputParametersSection(descriptionBuilder, apiDescription);
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Respostas e erros esperados");
        AppendResponsesSection(descriptionBuilder, operation);
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Observabilidade e rastreabilidade");
        descriptionBuilder.AppendLine("- Correlacionar chamadas pelo `correlationId` retornado nos headers/logs da API.");
        descriptionBuilder.AppendLine("- Falhas de validacao e negocio devem ser auditadas no monitoramento administrativo.");
        descriptionBuilder.AppendLine("- Em incidentes, registrar metodo e rota: `" + httpMethod + " " + relativePath + "`.");
        descriptionBuilder.AppendLine();

        descriptionBuilder.AppendLine("### Boas praticas de consumo");
        descriptionBuilder.AppendLine("- Enviar apenas campos do contrato para evitar rejeicao por validacao.");
        descriptionBuilder.AppendLine("- Respeitar paginação/filtros quando disponiveis para reduzir custo de consulta.");
        descriptionBuilder.AppendLine("- Tratar codigos `401`, `403`, `409` e `429` explicitamente no cliente.");

        var existingDescription = operation.Description?.Trim();
        if (!string.IsNullOrWhiteSpace(existingDescription))
        {
            descriptionBuilder.AppendLine();
            descriptionBuilder.AppendLine("### Notas complementares");
            descriptionBuilder.AppendLine(existingDescription);
        }

        operation.Description = descriptionBuilder.ToString().Trim();
    }

    private static void AppendAuthSection(StringBuilder builder, ApiDescription apiDescription)
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
            return;
        }

        builder.AppendLine("- Requer JWT Bearer no header `Authorization`.");
        builder.AppendLine("- Formato: `Authorization: Bearer {token}`.");

        if (authorizePolicies.Length == 0)
        {
            builder.AppendLine("- Sem policy explicita no endpoint (segue regras gerais de autenticacao).");
            return;
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
    }

    private static void AppendInputParametersSection(StringBuilder builder, ApiDescription apiDescription)
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

        if (pathParameters.Length == 0 && queryParameters.Length == 0 && headerParameters.Length == 0 && bodyParameters.Length == 0)
        {
            builder.AppendLine("- Endpoint sem parametros de entrada.");
            return;
        }

        if (pathParameters.Length > 0)
        {
            builder.AppendLine("- **Path**:");
            foreach (var parameter in pathParameters)
            {
                builder.AppendLine($"  - `{parameter.Name}` ({ResolveType(parameter.Type)}): identificador no caminho da rota.");
            }
        }

        if (queryParameters.Length > 0)
        {
            builder.AppendLine("- **Query**:");
            foreach (var parameter in queryParameters)
            {
                builder.AppendLine($"  - `{parameter.Name}` ({ResolveType(parameter.Type)}): filtro/controle de consulta.");
            }
        }

        if (headerParameters.Length > 0)
        {
            builder.AppendLine("- **Header**:");
            foreach (var parameter in headerParameters)
            {
                builder.AppendLine($"  - `{parameter.Name}` ({ResolveType(parameter.Type)}): metadado tecnico da requisicao.");
            }
        }

        if (bodyParameters.Length > 0)
        {
            builder.AppendLine("- **Body**:");
            foreach (var parameter in bodyParameters)
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
                : "Resposta de erro/sucesso conforme contrato do endpoint.";

            var description = string.IsNullOrWhiteSpace(response.Value.Description)
                ? mappedGuidance
                : response.Value.Description;

            builder.AppendLine($"- **{response.Key}**: {description}");
        }
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

    private static string InferResourceLabel(ApiDescription apiDescription)
    {
        var actionDescriptor = apiDescription.ActionDescriptor;
        var controllerName = actionDescriptor.RouteValues.TryGetValue("controller", out var routeControllerName)
            ? routeControllerName
            : actionDescriptor.DisplayName;

        if (!string.IsNullOrWhiteSpace(controllerName))
        {
            return SplitPascalCase(controllerName!);
        }

        var fallbackPath = NormalizeRelativePath(apiDescription.RelativePath).Trim('/');
        if (string.IsNullOrWhiteSpace(fallbackPath))
        {
            return "recursos da API";
        }

        return fallbackPath.Replace("/", " / ", StringComparison.Ordinal);
    }

    private static string SplitPascalCase(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return "recursos da API";
        }

        var builder = new StringBuilder();
        for (var i = 0; i < input.Length; i++)
        {
            var current = input[i];
            if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(input[i - 1]))
            {
                builder.Append(' ');
            }

            builder.Append(char.ToLowerInvariant(current));
        }

        return builder.ToString().Replace('-', ' ');
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
}
