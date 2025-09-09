using Application.DTOs.Auth;

public interface IAuthService
{
    Task<AuthResponseDto> LoginAsync(LoginRequest request);
    Task<AuthResponseDto> RegisterAsync(RegisterRequest request);
}