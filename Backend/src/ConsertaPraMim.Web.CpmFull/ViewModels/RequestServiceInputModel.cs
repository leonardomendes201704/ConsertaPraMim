using System.ComponentModel.DataAnnotations;

namespace AppMobileCPM.ViewModels;

public sealed class RequestServiceInputModel
{
    [Required(ErrorMessage = "Selecione uma categoria.")]
    public string CategoryId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Descreva o problema.")]
    [MinLength(10, ErrorMessage = "A descricao deve ter pelo menos 10 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a localizacao.")]
    [MinLength(3, ErrorMessage = "A localizacao deve ter pelo menos 3 caracteres.")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu nome.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu telefone.")]
    [Phone(ErrorMessage = "Telefone invalido.")]
    public string Phone { get; set; } = string.Empty;

    public bool IsWhatsapp { get; set; }
}
