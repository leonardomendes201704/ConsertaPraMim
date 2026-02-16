using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Application.Configuration;

public sealed record DisputeReasonDefinition(
    string Code,
    DisputeCaseType Type,
    string Title,
    string Description,
    bool RequiresEvidence = true);

public static class DisputeReasonTaxonomy
{
    private static readonly IReadOnlyList<DisputeReasonDefinition> Items = new List<DisputeReasonDefinition>
    {
        new("SERVICE_NOT_RESOLVED", DisputeCaseType.ServiceQuality, "Servico nao resolveu o problema", "Problema original persiste apos atendimento."),
        new("SERVICE_INCOMPLETE", DisputeCaseType.ServiceQuality, "Servico incompleto", "Escopo combinado nao foi entregue totalmente."),
        new("SERVICE_DAMAGE", DisputeCaseType.ServiceQuality, "Dano durante atendimento", "Houve dano em item/local durante a execucao."),
        new("CHARGED_DIFFERENT_AMOUNT", DisputeCaseType.Billing, "Valor cobrado divergente", "Valor cobrado diverge da aprovacao registrada."),
        new("UNAUTHORIZED_EXTRA_CHARGE", DisputeCaseType.Billing, "Cobranca adicional sem aprovacao", "Foi cobrado valor extra sem aceite formal."),
        new("PAYMENT_ISSUE", DisputeCaseType.Billing, "Falha no fluxo de pagamento", "Problema no pagamento, comprovante ou estorno."),
        new("MISCONDUCT", DisputeCaseType.Conduct, "Conduta inadequada", "Relato de comportamento inadequado durante a visita."),
        new("COMMUNICATION_BREAKDOWN", DisputeCaseType.Conduct, "Falha grave de comunicacao", "Nao houve comunicacao minima para conclusao segura."),
        new("CLIENT_NO_SHOW_DISPUTE", DisputeCaseType.NoShow, "Contestacao de no-show do cliente", "Prestador contesta classificacao de no-show do cliente."),
        new("PROVIDER_NO_SHOW_DISPUTE", DisputeCaseType.NoShow, "Contestacao de no-show do prestador", "Cliente contesta classificacao de no-show do prestador."),
        new("OTHER", DisputeCaseType.Other, "Outro motivo", "Motivo nao contemplado nas categorias padrao.")
    };

    public static IReadOnlyList<DisputeReasonDefinition> GetAll()
    {
        return Items;
    }

    public static IReadOnlyList<DisputeReasonDefinition> GetByType(DisputeCaseType type)
    {
        return Items
            .Where(item => item.Type == type)
            .ToList();
    }

    public static bool IsValid(DisputeCaseType type, string? reasonCode)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            return false;
        }

        var normalized = reasonCode.Trim().ToUpperInvariant();
        return Items.Any(item => item.Type == type && string.Equals(item.Code, normalized, StringComparison.OrdinalIgnoreCase));
    }
}
