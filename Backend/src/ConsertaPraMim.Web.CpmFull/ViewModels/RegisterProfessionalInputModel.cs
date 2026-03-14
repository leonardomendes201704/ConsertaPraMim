using System.ComponentModel.DataAnnotations;

namespace AppMobileCPM.ViewModels;

public sealed class RegisterProfessionalInputModel
{
    [Required(ErrorMessage = "Informe seu nome.")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe sua profissao.")]
    public string Profession { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe os servicos.")]
    public string Services { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o CEP.")]
    [RegularExpression(@"^\d{5}-?\d{3}$", ErrorMessage = "CEP invalido. Use 00000-000.")]
    public string PostalCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe seu telefone.")]
    [RegularExpression(@"^\(?\d{2}\)?\s?9\d{4}-?\d{4}$", ErrorMessage = "Telefone invalido. Use (00) 90000-0000.")]
    public string Phone { get; set; } = string.Empty;

    public bool IsWhatsapp { get; set; }

    [Required(ErrorMessage = "Descreva sua experiencia.")]
    [MinLength(10, ErrorMessage = "Informe ao menos 10 caracteres.")]
    public string Experience { get; set; } = string.Empty;
}
