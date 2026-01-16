using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using DiagnosticScenarios.Resources;

namespace DiagnosticScenarios.Controllers
{
    /// <summary>
    /// WebUIを提供するコントローラー
    /// 診断シナリオを操作するためのユーザーインターフェースを提供します
    /// </summary>
    public class HomeController : Controller
    {
        private readonly IStringLocalizer<SharedResources> _localizer;

        public HomeController(IStringLocalizer<SharedResources> localizer)
        {
            _localizer = localizer;
        }

        /// <summary>
        /// トップページを表示します
        /// 即座実行型シナリオのUIを提供します
        /// </summary>
        /// <returns>Index ビュー</returns>
        public IActionResult Index()
        {
            return View();
        }

        /// <summary>
        /// トグル型シナリオの管理ページを表示します
        /// バックグラウンドで実行されるシナリオの開始・停止・状態確認を行うUIを提供します
        /// </summary>
        /// <returns>ToggleScenarios ビュー</returns>
        public IActionResult ToggleScenarios()
        {
            return View();
        }

        /// <summary>
        /// エラーページを表示します
        /// </summary>
        /// <returns>Error ビュー</returns>
        public IActionResult Error()
        {
            return View();
        }

        /// <summary>
        /// ローカライゼーションのテスト用エンドポイント
        /// </summary>
        /// <returns>ローカライズされた文字列</returns>
        [HttpGet("/test-localization")]
        public IActionResult TestLocalization()
        {
            var title = _localizer["Nav.Title"];
            var home = _localizer["Nav.Home"];
            return Content($"Nav.Title = {title.Value}, Nav.Home = {home.Value}, ResourceNotFound: Title={title.ResourceNotFound}, Home={home.ResourceNotFound}");
        }
    }
}
