using Domain.Models;
using Infra.dbContext;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class AuthService : IAuthService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if(string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            throw new Exception("Email e senha são obrigatórios.");
        }
        var user = await _context.Usuarios
            .FirstOrDefaultAsync(u => u.Email == request.Email);

        if (user == null)
        {
            return new AuthResponse { Sucesso = false, Mensagem = "Usuário não encontrado." };
        }

        var passwordHasher = new PasswordHasher<Usuario>();
        var result = passwordHasher.VerifyHashedPassword(user, user.SenhaHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new Exception("Credenciais inválidas.");
        }

        var token = GenerateJwtToken(user);

        return new AuthResponse 
        { 
            Sucesso = true, 
            Token = token,
            Mensagem = "Login realizado com sucesso!" 
        };
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Password))
        {
            throw new Exception("Email e senha são obrigatórios.");
        }

        if (await _context.Usuarios.AnyAsync(u => u.Email == request.Email))
        {

            return new AuthResponse { Sucesso = false, Mensagem = "Email já cadastrado." };
        }
        var passwordHasher = new PasswordHasher<Usuario>();


        var user = new Usuario
        {
            Nome = request.Nome,
            Email = request.Email,
            DataCriacao = DateTime.UtcNow,
            DataAtualizacao = DateTime.UtcNow
        };

        user.SenhaHash = passwordHasher.HashPassword(user, request.Password);

        await _context.Usuarios.AddAsync(user);
        await _context.SaveChangesAsync();

        var token = GenerateJwtToken(user);

        return new AuthResponse 
        { 
            Sucesso = true, 
            Token = token,
            Mensagem = "Usuário criado com sucesso!" 
        };
    }

    private string GenerateJwtToken(Usuario user)
    {
        var jwtSettings = _configuration.GetSection("JwtSettings");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings["SecretKey"]!));

        var claims = new List<Claim>
        {
            new Claim("codeVerify", user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("email", user.Email)
        };

        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expires = DateTime.UtcNow.AddMinutes(Convert.ToDouble(jwtSettings["ExpiresInMinutes"]));

        var token = new JwtSecurityToken(
            issuer: jwtSettings["Issuer"],
            audience: jwtSettings["Audience"],
            claims: claims,
            expires: expires,
            signingCredentials: creds
        );
        var tokeyGenerate = new JwtSecurityTokenHandler().WriteToken(token);
        return tokeyGenerate;
    }
}