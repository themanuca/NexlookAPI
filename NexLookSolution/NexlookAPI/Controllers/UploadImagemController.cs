using Application.DTOs;
using Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace NexlookAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadImagemController : ControllerBase
    {
        private readonly ILogger<UploadImagemController> _logger;
        private readonly IUploadImagemService _uploadImagemService;
        public UploadImagemController(IUploadImagemService uploadImagemService)
        {
            _uploadImagemService = uploadImagemService;

        }
        [HttpPost]
        public Task<IActionResult> UploadImagem([FromForm] RoupaItem roupaItem)
        {
            return _uploadImagemService.UploadImagemAsync(roupaItem)
                .ContinueWith<IActionResult>(task =>
                {
                    if (task.Result.Sucesso)
                    {
                        return Ok(task.Result);}
                    else
                    {
                        return BadRequest(task.Result);
                    }
                });
        }
    }
}
