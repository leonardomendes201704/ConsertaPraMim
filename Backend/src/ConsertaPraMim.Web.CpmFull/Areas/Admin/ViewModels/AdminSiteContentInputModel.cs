using System.ComponentModel.DataAnnotations;

namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminSiteContentInputModel
{
    public int Id { get; set; }

    [StringLength(120, ErrorMessage = "A chave deve ter no maximo 120 caracteres.")]
    public string Key { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o valor do conteudo.")]
    public string Value { get; set; } = string.Empty;

    [StringLength(260, ErrorMessage = "A descricao deve ter no maximo 260 caracteres.")]
    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
