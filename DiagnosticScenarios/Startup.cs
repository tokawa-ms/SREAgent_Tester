using System;
using System.Globalization;
using System.IO;
using System.Reflection;
using DiagnosticScenarios.Services;
using DiagnosticScenarios.Resources;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Localization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Localization;
using Microsoft.OpenApi;

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
            // ローカライゼーションサポートの追加
            services.AddLocalization();
            
            services.AddControllersWithViews()
                .AddNewtonsoftJson()
                .AddViewLocalization()
                .AddDataAnnotationsLocalization();

            services.AddHttpContextAccessor();
            services.AddHttpClient();

            services.AddSingleton<IScenarioToggleService, ScenarioToggleService>();

            // サポートするカルチャーの設定
            var supportedCultures = new[]
            {
                new CultureInfo("ja"),
                new CultureInfo("en")
            };

            services.Configure<RequestLocalizationOptions>(options =>
            {
                options.DefaultRequestCulture = new RequestCulture("ja");
                options.SupportedCultures = supportedCultures;
                options.SupportedUICultures = supportedCultures;
                
                // クッキーベースのロケール選択を優先
                options.RequestCultureProviders.Insert(0, new CookieRequestCultureProvider
                {
                    CookieName = "SREAgentTester.Culture"
                });
            });

            // Swagger/OpenAPIの構成
            services.AddSwaggerGen(options =>
            {
                options.SwaggerDoc("v1", new OpenApiInfo
                {
                    Title = "SRE Agent Tester API",
                    Version = "v1",
                    Description = "SREエージェントのテストと検証のための診断シナリオAPI。" +
                                  "負荷テスト、障害注入、リソース使用のシミュレーションが可能です。",
                    Contact = new OpenApiContact
                    {
                        Name = "SRE Agent Tester Project"
                    }
                });

                // XML コメントファイルのパスを設定
                var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
                var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
                if (File.Exists(xmlPath))
                {
                    options.IncludeXmlComments(xmlPath);
                }
            });
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

            // ローカライゼーションミドルウェアの有効化
            app.UseRequestLocalization();

            app.UseRouting();

            app.UseAuthorization();

            // Swagger UIを有効化
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "SRE Agent Tester API v1");
                options.RoutePrefix = "swagger"; // /swagger でアクセス可能
                options.DocumentTitle = "SRE Agent Tester API Documentation";
            });

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
