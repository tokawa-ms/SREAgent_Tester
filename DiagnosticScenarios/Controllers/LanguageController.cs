using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using System;

namespace DiagnosticScenarios.Controllers
{
    /// <summary>
    /// 言語切り替え機能を提供するコントローラー
    /// </summary>
    [Route("[controller]")]
    public class LanguageController : Controller
    {
        /// <summary>
        /// 言語を変更し、クッキーに保存します
        /// </summary>
        /// <param name="culture">言語コード（ja または en）</param>
        /// <param name="returnUrl">リダイレクト先URL（省略時はリファラーまたはホームページ）</param>
        /// <returns>指定されたURLへのリダイレクト</returns>
        [HttpPost("SetLanguage")]
        [IgnoreAntiforgeryToken]
        public IActionResult SetLanguage([FromForm] string culture, [FromForm] string? returnUrl = null)
        {
            if (string.IsNullOrEmpty(culture))
            {
                return BadRequest("Culture parameter is required");
            }

            // サポートされている言語のみ許可
            if (culture != "ja" && culture != "en")
            {
                return BadRequest("Unsupported culture. Supported cultures are: ja, en");
            }

            Response.Cookies.Append(
                "SREAgentTester.Culture",
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    Path = "/",
                    SameSite = SameSiteMode.Lax
                }
            );

            // returnUrlが指定されていない場合は、リファラーまたはホームページにリダイレクト
            if (string.IsNullOrEmpty(returnUrl))
            {
                returnUrl = Request.Headers["Referer"].ToString();
                if (string.IsNullOrEmpty(returnUrl))
                {
                    returnUrl = "/";
                }
            }

            return LocalRedirect(returnUrl);
        }

        /// <summary>
        /// 現在の言語設定を取得します
        /// </summary>
        /// <returns>現在の言語コード</returns>
        [HttpGet("GetCurrentLanguage")]
        public IActionResult GetCurrentLanguage()
        {
            var feature = HttpContext.Features.Get<IRequestCultureFeature>();
            var currentCulture = feature?.RequestCulture.Culture.TwoLetterISOLanguageName ?? "ja";
            
            return Ok(new { language = currentCulture });
        }
    }
}
