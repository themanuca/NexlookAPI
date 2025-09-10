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
        public IAIServiceController(IAIService iaIService)
        {
            _iaIService = iaIService;
        }

        private Guid GetUserId()
        {
            var sub = User.FindFirstValue("codeVerify")
                      ?? throw new UnauthorizedAccessException("Token JWT inválido ou não contém o claim 'sub'.");

            return Guid.Parse(sub);
        }


        [HttpPost("GerarDescricaoImagem")]
        public async Task<IActionResult> GerarDescricaoImagem([FromBody] GerarDescricaoImagemRequest prompt)
        {
            var userId = GetUserId();

            var descricao = await _iaIService.GerarDescricaoImagemAsync(userId, prompt.PromptUsuario);
            return Ok(new { Descricao = descricao });
        }
        [HttpPost("GerarDescricaoImagemcomFoto")]
        public async Task<IActionResult> GerarDescricaoImagemComFoto([FromBody] GerarDescricaoImagemRequest prompt)
        {
            var userId = GetUserId();

            var descricao = await _iaIService.GerarDescricaoImagemcomFOTOAsync(userId, prompt.PromptUsuario);
            return Ok(new { Descricao = descricao });
        }
    }
}
