using System.ComponentModel.DataAnnotations;

namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminKanbanLeadInputModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe o tipo de funil.")]
    [StringLength(30)]
    public string BoardType { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Selecione uma etapa valida.")]
    public int StageId { get; set; }

    [Required(ErrorMessage = "Informe o nome do lead.")]
    [StringLength(140)]
    public string Name { get; set; } = string.Empty;

    [StringLength(30)]
    public string Phone { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "E-mail invalido.")]
    [StringLength(180)]
    public string Email { get; set; } = string.Empty;

    [StringLength(140)]
    public string ServiceCategory { get; set; } = string.Empty;

    [RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "CEP invalido.")]
    public string PostalCode { get; set; } = string.Empty;

    [StringLength(120)]
    public string City { get; set; } = string.Empty;

    [StringLength(120)]
    public string Source { get; set; } = string.Empty;

    [RegularExpression("^(alta|normal|baixa)$", ErrorMessage = "Prioridade invalida.")]
    public string Priority { get; set; } = "normal";

    [StringLength(500)]
    public string StatusNote { get; set; } = string.Empty;

    public string InternalNotes { get; set; } = string.Empty;

    public DateTime? LastContactAt { get; set; }
}

public sealed class AdminKanbanLeadNoteInputModel
{
    [Range(1, int.MaxValue)]
    public int LeadId { get; set; }

    [Required(ErrorMessage = "Informe a anotacao.")]
    [StringLength(3000)]
    public string Note { get; set; } = string.Empty;
}

public sealed class AdminKanbanOrderInputModel
{
    [Required]
    [StringLength(30)]
    public string BoardType { get; set; } = string.Empty;

    public int? ChangedLeadId { get; set; }
    public int? FromStageId { get; set; }
    public int? ToStageId { get; set; }

    public List<AdminKanbanStageOrderInputModel> Stages { get; set; } = [];
}

public sealed class AdminKanbanStageOrderInputModel
{
    public int StageId { get; set; }
    public List<int> LeadIds { get; set; } = [];
}
