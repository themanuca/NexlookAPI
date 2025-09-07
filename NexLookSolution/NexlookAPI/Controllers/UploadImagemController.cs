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

        public UploadImagemController(IUploadImagemService uploadImagemService, ILogger<UploadImagemController> logger) // Fix: Specify the generic type for ILogger
        {
            _uploadImagemService = uploadImagemService;
            _logger = logger;
        }
        private Guid GetUserId()
        {
            var sub = User.FindFirstValue("codeVerify")
                      ?? throw new UnauthorizedAccessException("Token JWT inválido ou não contém o claim 'sub'.");

            return Guid.Parse(sub);
        }


        [HttpPost("Uploado Imagem")]
        public Task<IActionResult> UploadImagem([FromForm] RoupaItem roupaItem)
        {
            var userId = GetUserId();
            return _uploadImagemService.UploadImagemAsync(roupaItem)
                .ContinueWith<IActionResult>(task =>
                {
                    if (task.Result.Sucesso)
                    {
                        return Ok(task.Result);
                    }
                    else
                    {
                        return BadRequest(task.Result);
                    }
                });
        }
    }
}
