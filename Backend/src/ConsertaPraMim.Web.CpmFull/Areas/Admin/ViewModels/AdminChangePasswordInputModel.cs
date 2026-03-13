using System.ComponentModel.DataAnnotations;

namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminChangePasswordInputModel
{
    [Required(ErrorMessage = "Informe a senha atual.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a nova senha.")]
    [MinLength(6, ErrorMessage = "A nova senha deve ter ao menos 6 caracteres.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Confirme a nova senha.")]
    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "A confirmacao nao confere.")]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}
