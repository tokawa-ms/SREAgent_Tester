using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DiagnosticScenarios.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DiagnosticScenarios
{
    /// <summary>
    /// ASP.NET Coreアプリケーションの起動と構成を管理するクラス
    /// 依存性注入コンテナの設定とHTTPリクエストパイプラインの構築を担当します
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// Startupクラスのコンストラクタ
        /// </summary>
        /// <param name="configuration">アプリケーション設定（appsettings.jsonなど）</param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// アプリケーション設定へのアクセスを提供します
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// 依存性注入コンテナにサービスを登録します
        /// ランタイムによって自動的に呼び出されます
        /// </summary>
        /// <param name="services">サービスコレクション（DIコンテナ）</param>
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews()
                .AddNewtonsoftJson();

            services.AddHttpContextAccessor();
            services.AddHttpClient();

            services.AddSingleton<IScenarioToggleService, ScenarioToggleService>();
        }

        /// <summary>
        /// HTTPリクエストパイプラインを構成します
        /// ランタイムによって自動的に呼び出されます
        /// </summary>
        /// <param name="app">アプリケーションビルダー（ミドルウェアパイプラインの構築に使用）</param>
        /// <param name="env">実行環境情報（Development、Production等）</param>
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                // app.UseHsts(); // Dockerコンテナでは無効化
            }

            // HTTPSリダイレクトをDockerコンテナでは無効化
            // app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                endpoints.MapControllers();
            });
        }
    }
}
