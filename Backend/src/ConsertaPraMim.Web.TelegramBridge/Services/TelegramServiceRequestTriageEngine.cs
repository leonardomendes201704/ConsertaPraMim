using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using ConsertaPraMim.Web.TelegramBridge.Models;

namespace ConsertaPraMim.Web.TelegramBridge.Services;

public sealed class TelegramServiceRequestTriageEngine
{
    private static readonly Regex ZipRegex = new("\\b(\\d{5})-?(\\d{3})\\b", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public TelegramServiceRequestTriageDecision Evaluate(
        TelegramChatbotConversationHistoryDto? history,
        TelegramChatbotAssistantReply aiReply,
        ChatMessageDto clientMessage)
    {
        var previousState = TryReadLatestTriageState(history);
        var mergedState = MergeState(previousState, aiReply.EntitiesJson, clientMessage.Text);

        var normalizedIntent = NormalizeIntent(aiReply.Intent);
        var isTriageIntent =
            normalizedIntent is "openservicerequest" or "open_service_request" or "triageproblem" or "triage_problem"
            || previousState is not null;

        if (!isTriageIntent)
        {
            return new TelegramServiceRequestTriageDecision(
                IsTriageIntent: false,
                State: mergedState,
                MissingFields: [],
                FollowUpMessage: null,
                CreatePayload: null);
        }

        var missingFields = ResolveMissingFields(mergedState);
        if (missingFields.Count > 0)
        {
            return new TelegramServiceRequestTriageDecision(
                IsTriageIntent: true,
                State: mergedState,
                MissingFields: missingFields,
                FollowUpMessage: BuildFollowUpMessage(missingFields[0]),
                CreatePayload: null);
        }

        if (mergedState.ServiceRequestId.HasValue)
        {
            return new TelegramServiceRequestTriageDecision(
                IsTriageIntent: true,
                State: mergedState,
                MissingFields: [],
                FollowUpMessage: null,
                CreatePayload: null);
        }

        var payload = new TelegramServiceRequestCreatePayload(
            Category: mergedState.CategoryEnum!,
            Description: BuildDescription(mergedState),
            Zip: mergedState.ZipCode!,
            Street: mergedState.Street ?? string.Empty,
            City: mergedState.City ?? string.Empty,
            Latitude: 0,
            Longitude: 0);

        return new TelegramServiceRequestTriageDecision(
            IsTriageIntent: true,
            State: mergedState,
            MissingFields: [],
            FollowUpMessage: null,
            CreatePayload: payload);
    }

    public TelegramServiceRequestTriageState MarkRequestCreated(
        TelegramServiceRequestTriageState state,
        Guid requestId,
        DateTime nowUtc)
    {
        return state with
        {
            ServiceRequestId = requestId,
            ServiceRequestCreatedAtUtc = nowUtc,
            LastUpdatedAtUtc = nowUtc
        };
    }

    public string SerializeState(TelegramServiceRequestTriageState state)
    {
        return JsonSerializer.Serialize(new
        {
            state
        }, JsonOptions);
    }

    public string? SerializeEntitiesFromState(TelegramServiceRequestTriageState state)
    {
        var payload = new
        {
            category = state.CategoryRaw,
            categoryEnum = state.CategoryEnum,
            problemDescription = state.ProblemDescription,
            equipment = state.Equipment,
            brand = state.Brand,
            model = state.Model,
            errorCode = state.ErrorCode,
            zipCode = state.ZipCode,
            street = state.Street,
            city = state.City,
            availability = state.Availability,
            serviceRequestId = state.ServiceRequestId
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    public string BuildCreatedConfirmationMessage(
        TelegramServiceRequestTriageState state,
        Guid requestId)
    {
        var categoryText = string.IsNullOrWhiteSpace(state.CategoryRaw)
            ? "servico"
            : state.CategoryRaw!.Trim();
        var problemText = string.IsNullOrWhiteSpace(state.ProblemDescription)
            ? "problema informado"
            : state.ProblemDescription!.Trim();
        var location = string.IsNullOrWhiteSpace(state.ZipCode)
            ? "sua regiao"
            : $"CEP {state.ZipCode}";

        return $"Perfeito! Registrei seu pedido #{requestId.ToString("N")[..8]} de {categoryText}. " +
               $"Resumo: {problemText}. Local: {location}. " +
               "Em seguida vou buscar prestadores compativeis na sua area e te orientar com os proximos passos.";
    }

    private static TelegramServiceRequestTriageState MergeState(
        TelegramServiceRequestTriageState? previousState,
        string? entitiesJson,
        string? clientMessage)
    {
        var entities = ParseEntities(entitiesJson);
        var nowUtc = DateTime.UtcNow;

        var categoryRaw = FirstNonEmpty(
            GetEntity(entities, "category", "categoria", "serviceCategory", "tipoServico"),
            previousState?.CategoryRaw);

        var categoryEnum = FirstNonEmpty(
            TryNormalizeCategoryToEnum(categoryRaw),
            previousState?.CategoryEnum,
            TryNormalizeCategoryToEnum(GetEntity(entities, "categoryEnum", "categoriaEnum")));

        var problemDescription = FirstNonEmpty(
            GetEntity(entities,
                "problemDescription",
                "problema",
                "defeito",
                "issue",
                "descricaoProblema",
                "problemSummary"),
            previousState?.ProblemDescription,
            TryBuildProblemFromClientMessage(clientMessage));

        var equipment = FirstNonEmpty(
            GetEntity(entities, "equipment", "equipamento", "appliance", "device"),
            previousState?.Equipment);

        if (string.IsNullOrWhiteSpace(categoryEnum))
        {
            categoryEnum = TryNormalizeCategoryToEnum(FirstNonEmpty(equipment, problemDescription));
        }

        var brand = FirstNonEmpty(
            GetEntity(entities, "brand", "marca"),
            previousState?.Brand);

        var model = FirstNonEmpty(
            GetEntity(entities, "model", "modelo"),
            previousState?.Model);

        var errorCode = FirstNonEmpty(
            GetEntity(entities, "errorCode", "codigoErro", "error", "erro"),
            previousState?.ErrorCode);

        var zipFromEntities = GetEntity(entities, "zipCode", "cep", "zip", "zipcode");
        var zipFromClientMessage = ExtractZipCode(clientMessage);
        var zipCode = FirstNonEmpty(
            NormalizeZip(zipFromEntities),
            NormalizeZip(zipFromClientMessage),
            previousState?.ZipCode);

        var street = FirstNonEmpty(
            GetEntity(entities, "street", "rua", "logradouro"),
            previousState?.Street);

        var city = FirstNonEmpty(
            GetEntity(entities, "city", "cidade", "municipio"),
            previousState?.City);

        var availability = FirstNonEmpty(
            GetEntity(entities, "availability", "disponibilidade", "timeWindow", "horario"),
            previousState?.Availability);

        return new TelegramServiceRequestTriageState(
            CategoryRaw: categoryRaw,
            CategoryEnum: categoryEnum,
            ProblemDescription: problemDescription,
            Equipment: equipment,
            Brand: brand,
            Model: model,
            ErrorCode: errorCode,
            ZipCode: zipCode,
            Street: street,
            City: city,
            Availability: availability,
            ServiceRequestId: previousState?.ServiceRequestId,
            ServiceRequestCreatedAtUtc: previousState?.ServiceRequestCreatedAtUtc,
            LastUpdatedAtUtc: nowUtc,
            LastClientMessage: FirstNonEmpty(clientMessage, previousState?.LastClientMessage));
    }

    private static List<string> ResolveMissingFields(TelegramServiceRequestTriageState state)
    {
        var missing = new List<string>(capacity: 3);

        if (string.IsNullOrWhiteSpace(state.CategoryEnum))
        {
            missing.Add("category");
        }

        if (string.IsNullOrWhiteSpace(state.ProblemDescription) || state.ProblemDescription.Trim().Length < 10)
        {
            missing.Add("problem_description");
        }

        if (string.IsNullOrWhiteSpace(state.ZipCode))
        {
            missing.Add("zip_code");
        }

        return missing;
    }

    private static string BuildFollowUpMessage(string missingField)
    {
        return missingField switch
        {
            "category" => "Perfeito. Para eu abrir seu pedido agora, me diga a categoria do servico: eletrica, hidraulica, eletronicos, eletrodomesticos, alvenaria, limpeza ou outros.",
            "problem_description" => "Me descreva o problema com um pouco mais de detalhe (equipamento, defeito e se aparece algum codigo de erro).",
            "zip_code" => "Me informe o CEP do local do atendimento para eu registrar seu pedido corretamente.",
            _ => "Preciso de mais alguns dados para abrir seu pedido."
        };
    }

    private static string BuildDescription(TelegramServiceRequestTriageState state)
    {
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(state.ProblemDescription))
        {
            parts.Add(state.ProblemDescription.Trim());
        }

        var equipmentInfo = BuildEquipmentInfo(state.Equipment, state.Brand, state.Model);
        if (!string.IsNullOrWhiteSpace(equipmentInfo))
        {
            parts.Add($"Equipamento: {equipmentInfo}.");
        }

        if (!string.IsNullOrWhiteSpace(state.ErrorCode))
        {
            parts.Add($"Codigo de erro: {state.ErrorCode.Trim()}.");
        }

        if (!string.IsNullOrWhiteSpace(state.Availability))
        {
            parts.Add($"Disponibilidade informada: {state.Availability.Trim()}.");
        }

        var description = string.Join(" ", parts).Trim();
        if (description.Length >= 10)
        {
            return description.Length <= 1000
                ? description
                : description[..1000];
        }

        var fallback = string.IsNullOrWhiteSpace(state.LastClientMessage)
            ? "Cliente solicitou atendimento tecnico para avaliacao do problema."
            : state.LastClientMessage.Trim();

        return fallback.Length <= 1000
            ? fallback
            : fallback[..1000];
    }

    private static string BuildEquipmentInfo(string? equipment, string? brand, string? model)
    {
        var tokens = new[]
        {
            equipment?.Trim(),
            brand?.Trim(),
            model?.Trim()
        }.Where(value => !string.IsNullOrWhiteSpace(value));

        return string.Join(" ", tokens);
    }

    private static TelegramServiceRequestTriageState? TryReadLatestTriageState(TelegramChatbotConversationHistoryDto? history)
    {
        if (history is null || history.ContextSnapshots.Count == 0)
        {
            return null;
        }

        var latestSnapshot = history.ContextSnapshots
            .OrderByDescending(item => item.CapturedAtUtc)
            .FirstOrDefault(item => item.SnapshotType.Equals("service_request_triage_state", StringComparison.OrdinalIgnoreCase));

        if (latestSnapshot is null || string.IsNullOrWhiteSpace(latestSnapshot.ContextJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(latestSnapshot.ContextJson);
            var root = document.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("state", out var stateElement))
            {
                return ParseStateElement(stateElement);
            }

            return ParseStateElement(root);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static TelegramServiceRequestTriageState? ParseStateElement(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var requestIdRaw = GetJsonString(root, "serviceRequestId");
        var requestId = Guid.TryParse(requestIdRaw, out var parsedRequestId)
            ? parsedRequestId
            : (Guid?)null;

        var createdAtRaw = GetJsonString(root, "serviceRequestCreatedAtUtc");
        var createdAt = DateTime.TryParse(createdAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsedCreatedAt)
            ? parsedCreatedAt.ToUniversalTime()
            : (DateTime?)null;

        return new TelegramServiceRequestTriageState(
            CategoryRaw: GetJsonString(root, "categoryRaw"),
            CategoryEnum: GetJsonString(root, "categoryEnum"),
            ProblemDescription: GetJsonString(root, "problemDescription"),
            Equipment: GetJsonString(root, "equipment"),
            Brand: GetJsonString(root, "brand"),
            Model: GetJsonString(root, "model"),
            ErrorCode: GetJsonString(root, "errorCode"),
            ZipCode: GetJsonString(root, "zipCode"),
            Street: GetJsonString(root, "street"),
            City: GetJsonString(root, "city"),
            Availability: GetJsonString(root, "availability"),
            ServiceRequestId: requestId,
            ServiceRequestCreatedAtUtc: createdAt,
            LastUpdatedAtUtc: DateTime.UtcNow,
            LastClientMessage: GetJsonString(root, "lastClientMessage"));
    }

    private static Dictionary<string, string?> ParseEntities(string? entitiesJson)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(entitiesJson))
        {
            return result;
        }

        try
        {
            using var document = JsonDocument.Parse(entitiesJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return result;
            }

            foreach (var property in document.RootElement.EnumerateObject())
            {
                result[property.Name] = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString(),
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Object => property.Value.GetRawText(),
                    JsonValueKind.Array => property.Value.GetRawText(),
                    _ => null
                };
            }
        }
        catch (JsonException)
        {
            return result;
        }

        return result;
    }

    private static string? GetEntity(Dictionary<string, string?> entities, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!entities.TryGetValue(key, out var value))
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }

    private static string? GetJsonString(JsonElement root, string propertyName)
    {
        if (TryGetPropertyIgnoreCase(root, propertyName, out var value) is false)
        {
            return null;
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => null
        };
    }

    private static bool TryGetPropertyIgnoreCase(
        JsonElement root,
        string propertyName,
        out JsonElement value)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            value = default;
            return false;
        }

        if (root.TryGetProperty(propertyName, out value))
        {
            return true;
        }

        foreach (var property in root.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string? TryBuildProblemFromClientMessage(string? clientMessage)
    {
        if (string.IsNullOrWhiteSpace(clientMessage))
        {
            return null;
        }

        var trimmed = clientMessage.Trim();
        if (trimmed.Length < 10)
        {
            return null;
        }

        return trimmed.Length <= 600
            ? trimmed
            : trimmed[..600];
    }

    private static string? ExtractZipCode(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var match = ZipRegex.Match(text);
        return match.Success
            ? $"{match.Groups[1].Value}{match.Groups[2].Value}"
            : null;
    }

    private static string? NormalizeZip(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != 8)
        {
            return null;
        }

        return $"{digits[..5]}-{digits[5..]}";
    }

    private static string NormalizeIntent(string? intent)
    {
        if (string.IsNullOrWhiteSpace(intent))
        {
            return "unknown";
        }

        var chars = intent
            .Trim()
            .ToLowerInvariant()
            .Where(static c => char.IsLetterOrDigit(c) || c == '_');
        return string.Concat(chars);
    }

    private static string? TryNormalizeCategoryToEnum(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        var normalized = NormalizeToken(rawValue);

        return normalized switch
        {
            "electrical" => "Electrical",
            "eletrica" => "Electrical",
            "plumbing" => "Plumbing",
            "hidraulica" => "Plumbing",
            "electronics" => "Electronics",
            "eletronicos" => "Electronics",
            "appliances" => "Appliances",
            "eletrodomesticos" => "Appliances",
            "masonry" => "Masonry",
            "alvenaria" => "Masonry",
            "cleaning" => "Cleaning",
            "limpeza" => "Cleaning",
            "other" => "Other",
            "outros" => "Other",
            _ when normalized.Contains("arcondicionado") => "Appliances",
            _ when normalized.Contains("geladeira") => "Appliances",
            _ when normalized.Contains("microondas") => "Appliances",
            _ when normalized.Contains("fogao") => "Appliances",
            _ when normalized.Contains("maquinalavar") => "Appliances",
            _ when normalized.Contains("chuveiro") => "Electrical",
            _ when normalized.Contains("tomada") => "Electrical",
            _ when normalized.Contains("disjuntor") => "Electrical",
            _ when normalized.Contains("vazamento") => "Plumbing",
            _ when normalized.Contains("torneira") => "Plumbing",
            _ when normalized.Contains("encanamento") => "Plumbing",
            _ when normalized.Contains("televisao") => "Electronics",
            _ when normalized.Contains("notebook") => "Electronics",
            _ when normalized.Contains("celular") => "Electronics",
            _ when normalized.Contains("parede") => "Masonry",
            _ when normalized.Contains("reboco") => "Masonry",
            _ when normalized.Contains("pintura") => "Masonry",
            _ when normalized.Contains("faxina") => "Cleaning",
            _ => null
        };
    }

    private static string NormalizeToken(string value)
    {
        var lower = value.Trim().ToLowerInvariant();
        var normalized = lower
            .Replace("á", "a", StringComparison.Ordinal)
            .Replace("à", "a", StringComparison.Ordinal)
            .Replace("ã", "a", StringComparison.Ordinal)
            .Replace("â", "a", StringComparison.Ordinal)
            .Replace("é", "e", StringComparison.Ordinal)
            .Replace("ê", "e", StringComparison.Ordinal)
            .Replace("í", "i", StringComparison.Ordinal)
            .Replace("ó", "o", StringComparison.Ordinal)
            .Replace("ô", "o", StringComparison.Ordinal)
            .Replace("õ", "o", StringComparison.Ordinal)
            .Replace("ú", "u", StringComparison.Ordinal)
            .Replace("ç", "c", StringComparison.Ordinal);

        return string.Concat(normalized.Where(char.IsLetterOrDigit));
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return null;
    }
}

