using Application.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace NexlookAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UploadImagemController : ControllerBase
    {
       public UploadImagemController()
       {

       }
        [HttpPost]
        public Task<IActionResult> UploadImagem([FromForm] RoupaItem roupaItem)
        {
            return Task.FromResult((IActionResult)Ok(new { Message = "Imagem enviada com sucesso!" }));
        }
    }
}
