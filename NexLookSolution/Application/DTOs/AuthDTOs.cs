public record LoginRequest(string Email, string Password);
public record RegisterRequest
{
    public required string Nome { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
}
public record AuthResponse
{
    public bool Sucesso { get; set; }
    public string? Token { get; set; }
    public string? Mensagem { get; set; }
}