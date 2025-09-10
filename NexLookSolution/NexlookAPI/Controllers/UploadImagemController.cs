using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NexlookAPI.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UploadImagemController : ControllerBase
    {
        private readonly ILogger<UploadImagemController> _logger;
        private readonly IUploadImagemService _uploadImagemService;

        public UploadImagemController(IUploadImagemService uploadImagemService, ILogger<UploadImagemController> logger)
        {
            _uploadImagemService = uploadImagemService;
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

        [HttpPost("UploadImagem")]
        public async Task<IActionResult> UploadImagem([FromForm] RoupaItem roupaItem)
        {
            try
            {
                _logger.LogInformation("Iniciando upload de imagem. UserId: {UserId}", GetUserId());

                if (roupaItem == null || roupaItem.File == null)
                {
                    _logger.LogWarning("Tentativa de upload com dados inválidos");
                    return BadRequest("Dados da roupa ou imagem não fornecidos");
                }

                _logger.LogInformation("Processando upload de imagem. Tamanho: {Size} bytes",
                    roupaItem.File.Length);

                var userId = GetUserId();
                var result = await _uploadImagemService.UploadLookAsync(roupaItem, userId);

                if (result.Sucesso)
                {
                    _logger.LogInformation("Upload concluído com sucesso para usuário {UserId}", userId);
                    return Ok(result);
                }
                else
                {
                    _logger.LogWarning("Falha no upload para usuário {UserId}. Mensagem: {Message}",
                        userId, result.Mensagem);
                    return BadRequest(result);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Erro de autorização durante upload de imagem");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante upload de imagem");
                return StatusCode(500, "Ocorreu um erro interno ao processar o upload");
            }
        }

        [HttpGet("Imagens")]
        public async Task<IActionResult> GetAllImages()
        {
            try
            {
                _logger.LogInformation("Iniciando busca de imagens. UserId: {UserId}", GetUserId());

                var userId = GetUserId();
                var result = await _uploadImagemService.BuscarLooksUsuarioAsync(userId);

                _logger.LogInformation("Busca de imagens concluída para usuário {UserId}. Total de imagens: {Count}",
                    userId, result?.Count() ?? 0);

                return Ok(result);
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError(ex, "Erro de autorização durante busca de imagens");
                return Unauthorized(ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante busca de imagens");
                return StatusCode(500, "Ocorreu um erro interno ao buscar as imagens");
            }
        }
    }
}
