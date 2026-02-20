using System.ComponentModel.DataAnnotations;
using ConsertaPraMim.Application.DTOs;
using ConsertaPraMim.Domain.Enums;

namespace ConsertaPraMim.Web.Provider.Models;

public class SupportTicketFiltersViewModel
{
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public class SupportTicketsIndexViewModel
{
    public SupportTicketFiltersViewModel Filters { get; set; } = new();
    public MobileProviderSupportTicketListResponseDto? Response { get; set; }
    public string? ErrorMessage { get; set; }
}

public class SupportTicketCreateViewModel
{
    [Required(ErrorMessage = "Assunto e obrigatorio.")]
    [MaxLength(220, ErrorMessage = "Assunto deve ter no maximo 220 caracteres.")]
    public string Subject { get; set; } = string.Empty;

    [MaxLength(80, ErrorMessage = "Categoria deve ter no maximo 80 caracteres.")]
    public string? Category { get; set; }

    [Range(1, 4, ErrorMessage = "Prioridade invalida.")]
    public int? Priority { get; set; } = (int)SupportTicketPriority.Medium;

    [Required(ErrorMessage = "Mensagem inicial e obrigatoria.")]
    [MaxLength(3000, ErrorMessage = "Mensagem deve ter no maximo 3000 caracteres.")]
    public string InitialMessage { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public class SupportTicketDetailsViewModel
{
    public MobileProviderSupportTicketDetailsDto? Ticket { get; set; }
    public string? ErrorMessage { get; set; }
    public string? NewMessage { get; set; }
}
