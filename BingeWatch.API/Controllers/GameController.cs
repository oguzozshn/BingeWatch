using BingeWatch.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BingeWatch.API.Controllers
{
    [ApiController]
    [Route("api/game")]
    public class GameController : ControllerBase
    {
        private readonly IGameService _gameService;

        public GameController(IGameService gameService) => _gameService = gameService;

        /// <summary>
        /// Yeni bir el. Anonime açık: oyun kişisel veriye dokunmuyor ve
        /// giriş yapmamış ziyaretçiye uygulamayı tanıtan en ucuz yüzey.
        /// </summary>
        [HttpGet("round")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRound()
        {
            var round = await _gameService.GetRoundAsync();
            if (round == null)
                return StatusCode(503, new { message = "Oyun için yeterli dizi bulunamadı." });

            return Ok(round);
        }

        /// <summary>
        /// Üretilmiş trivia sorusu. Soru tipine göre bazen havuz yetmiyor
        /// (ör. aynı yılda başlayan diziler); o durumda 503 yerine yeniden
        /// denemek istemcinin işi — uç boş soru uydurmuyor.
        /// </summary>
        [HttpGet("trivia")]
        [AllowAnonymous]
        public async Task<IActionResult> GetTrivia()
        {
            var question = await _gameService.GetTriviaAsync();
            if (question == null)
                return StatusCode(503, new { message = "Soru üretilemedi." });

            return Ok(question);
        }
    }
}
