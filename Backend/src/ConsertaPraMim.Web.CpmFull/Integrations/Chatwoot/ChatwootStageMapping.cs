using AppMobileCPM.Services;

namespace AppMobileCPM.Integrations.Chatwoot;

public sealed class ChatwootStageMapping
{
    public required string ConversationStatus { get; init; }
    public required string StageSlug { get; init; }
    public required IReadOnlyList<string> Labels { get; init; }
}

public static class ChatwootStageMappings
{
    public static ChatwootStageMapping Resolve(string boardType, string stageName)
    {
        var normalizedBoardType = AdminKanbanBoardTypes.Normalize(boardType);
        var normalizedStageName = NormalizeStageName(stageName);
        var boardLabel = normalizedBoardType == AdminKanbanBoardTypes.Clients ? "cpm_clientes" : "cpm_prestadores";
        var stageSlug = $"{GetBoardSlug(normalizedBoardType)}_{normalizedStageName}";

        return new ChatwootStageMapping
        {
            ConversationStatus = ResolveConversationStatus(normalizedBoardType, normalizedStageName),
            StageSlug = stageSlug,
            Labels =
            [
                boardLabel,
                $"cpm_{stageSlug}"
            ]
        };
    }

    private static string ResolveConversationStatus(string boardType, string normalizedStageName)
    {
        if (boardType == AdminKanbanBoardTypes.Clients)
        {
            return normalizedStageName switch
            {
                "novo_lead" => "open",
                "tentativa_de_contato" => "pending",
                "agendado" => "pending",
                "em_atendimento" => "open",
                "concluido" => "resolved",
                "perdido" => "resolved",
                _ => "open"
            };
        }

        return normalizedStageName switch
        {
            "novo_cadastro" => "open",
            "primeiro_contato" => "pending",
            "documentacao_pendente" => "pending",
            "validacao_tecnica" => "pending",
            "ativo_na_plataforma" => "resolved",
            "inativo_recusado" => "resolved",
            _ => "open"
        };
    }

    private static string GetBoardSlug(string boardType) =>
        boardType == AdminKanbanBoardTypes.Clients ? "clientes" : "prestadores";

    private static string NormalizeStageName(string stageName)
    {
        if (string.IsNullOrWhiteSpace(stageName))
        {
            return "sem_etapa";
        }

        var normalized = stageName.Trim().ToLowerInvariant();
        var chars = normalized
            .Normalize(System.Text.NormalizationForm.FormD)
            .Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark)
            .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
            .ToArray();

        var collapsed = new string(chars);
        while (collapsed.Contains("__", StringComparison.Ordinal))
        {
            collapsed = collapsed.Replace("__", "_", StringComparison.Ordinal);
        }

        return collapsed.Trim('_');
    }
}
