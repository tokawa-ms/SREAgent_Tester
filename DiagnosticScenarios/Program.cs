using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace DiagnosticScenarios
{
    /// <summary>
    /// アプリケーションのエントリポイントクラス
    /// ASP.NET Core Webホストを構築し、起動します
    /// </summary>
    public class Program
    {
        /// <summary>
        /// アプリケーションのメインエントリポイント
        /// </summary>
        /// <param name="args">コマンドライン引数</param>
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        /// <summary>
        /// ASP.NET Core Webホストビルダーを作成します
        /// デフォルトの設定を使用し、Startupクラスで依存性注入とミドルウェアを構成します
        /// </summary>
        /// <param name="args">コマンドライン引数（設定のオーバーライドに使用可能）</param>
        /// <returns>構成されたIHostBuilderインスタンス</returns>
        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                });
    }
}
