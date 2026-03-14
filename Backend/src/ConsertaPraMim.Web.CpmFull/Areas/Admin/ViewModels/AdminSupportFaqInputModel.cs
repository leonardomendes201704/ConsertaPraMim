using System.ComponentModel.DataAnnotations;

namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminSupportFaqInputModel
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Informe a pergunta.")]
    [StringLength(300, ErrorMessage = "A pergunta deve ter no maximo 300 caracteres.")]
    public string Question { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a resposta.")]
    public string Answer { get; set; } = string.Empty;

    [Range(0, 9999, ErrorMessage = "Informe uma ordem valida.")]
    public int SortOrder { get; set; }

    public bool IsActive { get; set; } = true;
}
