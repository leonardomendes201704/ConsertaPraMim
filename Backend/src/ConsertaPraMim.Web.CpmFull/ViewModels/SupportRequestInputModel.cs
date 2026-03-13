using System.ComponentModel.DataAnnotations;

namespace AppMobileCPM.ViewModels;

public sealed class SupportRequestInputModel
{
    [Required(ErrorMessage = "Informe seu nome.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu e-mail.")]
    [EmailAddress(ErrorMessage = "E-mail invalido.")]
    public string Email { get; set; } = string.Empty;

    [RegularExpression(@"^\(?\d{2}\)?\s?9?\d{4}-?\d{4}$", ErrorMessage = "Telefone invalido.")]
    public string Phone { get; set; } = string.Empty;

    [Required(ErrorMessage = "Selecione uma categoria.")]
    public string Category { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o assunto.")]
    [MinLength(5, ErrorMessage = "O assunto deve ter ao menos 5 caracteres.")]
    public string Subject { get; set; } = string.Empty;

    [Required(ErrorMessage = "Descreva sua solicitacao.")]
    [MinLength(20, ErrorMessage = "A mensagem deve ter ao menos 20 caracteres.")]
    public string Message { get; set; } = string.Empty;
}
