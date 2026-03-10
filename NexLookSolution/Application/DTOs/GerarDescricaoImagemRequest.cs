using System.ComponentModel.DataAnnotations;

public class GerarDescricaoImagemRequest
{
    [Required(ErrorMessage = "O prompt é obrigatório.")]
    [StringLength(500, MinimumLength = 10, ErrorMessage = "O prompt deve ter entre 10 e 500 caracteres.")]
    public string PromptUsuario { get; set; }
}