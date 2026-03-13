using System.ComponentModel.DataAnnotations;

namespace AppMobileCPM.Areas.Admin.ViewModels;

public sealed class AdminLoginInputModel
{
    [Required(ErrorMessage = "Informe o usuario.")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe a senha.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
