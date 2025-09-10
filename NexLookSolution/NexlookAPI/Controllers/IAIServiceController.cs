using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using static Application.DTOs.IAdto.LookPromptDTO;

namespace NexlookAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class IAIServiceController : ControllerBase
    {
        private readonly IAIService _iaIService;
        private readonly ILogger<IAIServiceController> _logger;

        public IAIServiceController(IAIService iaIService, ILogger<IAIServiceController> logger)
        {
            _iaIService = iaIService;
            _logger = logger;
        }

        private Guid GetUserId()
        {
            try
            {
                var sub = User.FindFirstValue("codeVerify");
                if (string.IsNullOrEmpty(sub))
                {
                    _logger.LogError("Token JWT inválido ou não contém o claim 'codeVerify'");
                    throw new UnauthorizedAccessException("Token JWT inválido ou não contém o claim 'codeVerify'.");
                }

                return Guid.Parse(sub);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao extrair ID do usuário do token");
                throw;
            }
        }

        [HttpPost("GerarDescricaoImagem")]
        public async Task<IActionResult> GerarDescricaoImagem([FromBody] GerarDescricaoImagemRequest prompt)
        {
            try
            {
                _logger.LogInformation("Iniciando geração de descrição de imagem. UserId: {UserId}", GetUserId());
                
                if (prompt == null || string.IsNullOrWhiteSpace(prompt.PromptUsuario))
                {
                    _logger.LogWarning("Prompt inválido recebido");
                    return BadRequest("O prompt não pode estar vazio");
                }

                var userId = GetUserId();
                _logger.LogInformation("Processando prompt para usuário {UserId}. Prompt: {Prompt}", userId, prompt.PromptUsuario);

                var descricao = await _iaIService.GerarDescricaoImagemAsync(userId, prompt.PromptUsuario);
                
                _logger.LogInformation("Descrição gerada com sucesso para usuário {UserId}", userId);
                return Ok(new { Descricao = descricao });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Erro de autorização ao gerar descrição");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar descrição de imagem");
                return StatusCode(500, "Ocorreu um erro interno ao processar sua solicitação");
            }
        }

        [HttpPost("GerarDescricaoImagemcomFoto")]
        public async Task<IActionResult> GerarDescricaoImagemComFoto([FromBody] GerarDescricaoImagemRequest prompt)
        {
            try
            {
                _logger.LogInformation("Iniciando geração de descrição de imagem com foto. UserId: {UserId}", GetUserId());
                
                if (prompt == null || string.IsNullOrWhiteSpace(prompt.PromptUsuario))
                {
                    _logger.LogWarning("Prompt inválido recebido para geração com foto");
                    return BadRequest("O prompt não pode estar vazio");
                }

                var userId = GetUserId();
                _logger.LogInformation("Processando prompt com foto para usuário {UserId}. Prompt: {Prompt}", userId, prompt.PromptUsuario);

                var descricao = await _iaIService.GerarDescricaoImagemcomFOTOAsync(userId, prompt.PromptUsuario);
                
                _logger.LogInformation("Descrição com foto gerada com sucesso para usuário {UserId}", userId);
                return Ok(new { Descricao = descricao });
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Erro de autorização ao gerar descrição com foto");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao gerar descrição de imagem com foto: {Error}", ex.Message);
                return StatusCode(500, "Ocorreu um erro interno ao processar sua solicitação");
            }
        }
    }
}
