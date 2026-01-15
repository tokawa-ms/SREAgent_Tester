using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;

namespace DiagnosticScenarios.Controllers
{
    /// <summary>
    /// サンプルAPIコントローラー
    /// ASP.NET Core Web APIの基本的な動作を確認するためのテスト用エンドポイントです
    /// </summary>
    /// <remarks>
    /// このコントローラーは診断シナリオの一部ではなく、
    /// APIが正常に動作していることを確認するためのサンプルです
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    public class ValuesController : ControllerBase
    {
        /// <summary>
        /// 全ての値を取得します
        /// </summary>
        /// <returns>文字列の配列</returns>
        // GET api/values
        [HttpGet]
        public ActionResult<IEnumerable<string>> Get()
        {
            return new string[] { "value1", "value2" };
        }

        /// <summary>
        /// 指定されたIDの値を取得します
        /// </summary>
        /// <param name="id">取得する値のID</param>
        /// <returns>値の文字列</returns>
        // GET api/values/5
        [HttpGet("{id}")]
        public ActionResult<string> Get(int id)
        {
            return "value";
        }

        /// <summary>
        /// 新しい値を追加します
        /// </summary>
        /// <param name="value">追加する値</param>
        // POST api/values
        [HttpPost]
        public void Post([FromBody] string value)
        {
        }

        /// <summary>
        /// 指定されたIDの値を更新します
        /// </summary>
        /// <param name="id">更新する値のID</param>
        /// <param name="value">新しい値</param>
        // PUT api/values/5
        [HttpPut("{id}")]
        public void Put(int id, [FromBody] string value)
        {
        }

        /// <summary>
        /// 指定されたIDの値を削除します
        /// </summary>
        /// <param name="id">削除する値のID</param>
        // DELETE api/values/5
        [HttpDelete("{id}")]
        public void Delete(int id)
        {
        }
    }
}
