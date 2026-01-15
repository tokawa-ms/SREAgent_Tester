using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DiagnosticScenarios.Controllers
{
    /// <summary>
    /// 即座に実行される診断シナリオのAPIコントローラー
    /// デッドロック、CPU負荷、メモリスパイク、例外など、様々な問題をシミュレートします
    /// </summary>
    /// <remarks>
    /// このコントローラーのエンドポイントは即座に実行され、完了まで待機します
    /// 長時間実行するシナリオにはScenarioToggleControllerを使用してください
    /// </remarks>
    [Route("api/[controller]")]
    [ApiController]
    public class DiagScenarioController : ControllerBase
    {
        // メモリリーク許容範囲の定義
        private const int MinMemLeakKilobytes = 1;
        private const int MaxMemLeakKilobytes = 10240;

        // デッドロックシナリオで使用するロックオブジェクト
        private readonly object _deadlockLock1 = new();
        private readonly object _deadlockLock2 = new();
        // メモリリークシナリオで使用する静的シミュレータ（アプリケーション全体で共有）
        private static readonly MemoryLeakSimulator _memoryLeakSimulator = new();
        private readonly ILogger<DiagScenarioController> _logger;

        /// <summary>
        /// DiagScenarioControllerのコンストラクタ
        /// </summary>
        /// <param name="logger">ロガー（DIコンテナから注入）</param>
        public DiagScenarioController(ILogger<DiagScenarioController> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// デッドロックシナリオを実行します
        /// 複数のスレッドが互いにロックを待ち合い、デッドロック状態を発生させます
        /// </summary>
        /// <returns>成功メッセージ（ただし実際にはデッドロックが発生するためハングする可能性があります）</returns>
        /// <remarks>
        /// このエンドポイントは意図的にデッドロックを発生させ、応答しなくなる可能性があります
        /// デッドロック検出ツールやスレッドダンプのテストに使用してください
        /// </remarks>
        [HttpGet]
        [Route("deadlock")]
        public ActionResult<string> Deadlock()
        {
            var starterThread = new Thread(DeadlockFunc) { IsBackground = true };
            starterThread.Start();

            Thread.Sleep(5000);

            var threads = new Thread[300];
            for (int i = 0; i < threads.Length; i++)
            {
                threads[i] = new Thread(() =>
                {
                    lock (_deadlockLock1)
                    {
                        Thread.Sleep(100);
                    }
                })
                { IsBackground = true };
                threads[i].Start();
            }

            foreach (var thread in threads)
            {
                thread.Join();
            }

            return "success:deadlock";
        }

        /// <summary>
        /// デッドロックを発生させるヘルパーメソッド
        /// 2つのロックを逆順で取得しようとすることでデッドロックを引き起こします
        /// </summary>
        private void DeadlockFunc()
        {
            lock (_deadlockLock1)
            {
                var competingThread = new Thread(() =>
                {
                    lock (_deadlockLock2)
                    {
                        Monitor.Enter(_deadlockLock1);
                    }
                })
                { IsBackground = true };
                competingThread.Start();

                Thread.Sleep(2000);
                Monitor.Enter(_deadlockLock2);
            }
        }

        /// <summary>
        /// メモリスパイクシナリオを実行します
        /// 定期的に大量のオブジェクトを生成・破棄することで、メモリ使用量の急激な増減を引き起こします
        /// </summary>
        /// <param name="seconds">シナリオの実行時間（秒）。1～1800の範囲で指定</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功メッセージ</returns>
        /// <remarks>
        /// 5秒ごとにメモリ確保とGCを繰り返し、メモリプロファイラーやGC監視ツールのテストに使用します
        /// </remarks>
        [HttpGet]
        [Route("memspike/{seconds:int}")]
        public async Task<ActionResult<string>> MemSpike(int seconds, CancellationToken cancellationToken)
        {
            if (seconds < 1 || seconds > 1800)
            {
                return BadRequest("seconds must be between 1 and 1800.");
            }

            var deadline = DateTime.UtcNow.AddSeconds(seconds);

            while (DateTime.UtcNow < deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var processor = new Processor();
                const int iterations = 2_000_000;
                for (var i = 0; i < iterations; i++)
                {
                    processor.ProcessTransaction(new Customer(Guid.NewGuid().ToString()));
                }

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);

                processor = null;
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
            }

            return "success:memspike";
        }

        /// <summary>
        /// メモリリークシナリオを実行します
        /// 指定されたサイズのメモリを確保し、解放せずに保持することでメモリリークをシミュレートします
        /// </summary>
        /// <param name="kilobytes">確保するメモリサイズ（KB）。1～10240の範囲で指定</param>
        /// <returns>成功メッセージと確保したメモリサイズ</returns>
        /// <remarks>
        /// 確保されたメモリは静的フィールドで保持され、アプリケーションを再起動するまで解放されません
        /// メモリリーク検出ツールのテストに使用してください
        /// </remarks>
        [HttpGet]
        [Route("memleak/{kilobytes:int}")]
        public ActionResult<string> MemLeak(int kilobytes)
        {
            if (kilobytes < MinMemLeakKilobytes || kilobytes > MaxMemLeakKilobytes)
            {
                return BadRequest($"kilobytes must be between {MinMemLeakKilobytes} and {MaxMemLeakKilobytes}.");
            }

            _memoryLeakSimulator.Allocate(kilobytes);

            return $"success:memleak ({kilobytes}KB retained)";
        }

        /// <summary>
        /// 例外を発生させるシナリオ
        /// 単純に例外を投げて、エラーハンドリングやログ記録のテストに使用します
        /// </summary>
        /// <returns>このメソッドは常に例外を投げます</returns>
        /// <exception cref="Exception">常に発生します</exception>
        [HttpGet]
        [Route("exception")]
        public ActionResult<string> exception()
        {
            throw new Exception("bad, bad code");
        }

        /// <summary>
        /// 例外バーストシナリオを実行します
        /// 指定された期間、高頻度で例外を発生させて、エラーログやトレースの負荷テストに使用します
        /// </summary>
        /// <param name="durationSeconds">シナリオの実行時間（秒）。1～1800の範囲で指定</param>
        /// <param name="exceptionsPerSecond">1秒あたりの例外発生数。1～1000の範囲で指定</param>
        /// <returns>成功メッセージと発生させた例外の総数</returns>
        /// <remarks>
        /// 全ての例外はキャッチされ、ログに記録されます
        /// </remarks>
        [HttpGet]
        [Route("exceptionburst/{durationSeconds}/{exceptionsPerSecond}")]
        public async Task<ActionResult<string>> ExceptionBurst(int durationSeconds, int exceptionsPerSecond)
        {
            if (durationSeconds < 1 || durationSeconds > 1800)
            {
                return BadRequest("durationSeconds must be between 1 and 1800.");
            }

            if (exceptionsPerSecond < 1 || exceptionsPerSecond > 1000)
            {
                return BadRequest("exceptionsPerSecond must be between 1 and 1000.");
            }

            var totalExceptions = durationSeconds * exceptionsPerSecond;
            var endTime = DateTime.UtcNow.AddSeconds(durationSeconds);

            while (DateTime.UtcNow < endTime)
            {
                for (int i = 0; i < exceptionsPerSecond; i++)
                {
                    try
                    {
                        throw new InvalidOperationException("Burst exception scenario");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Burst exception iteration {Iteration}", i);
                    }
                }

                await Task.Delay(1000);
            }

            return $"success:exceptionburst ({totalExceptions} exceptions generated)";
        }

        /// <summary>
        /// 確率的負荷シナリオを実行します
        /// 指定された頻度でバックエンドリクエストをシミュレートし、一定確率で例外を発生させます
        /// </summary>
        /// <param name="durationSeconds">シナリオの実行時間（秒）。1～1800の範囲で指定</param>
        /// <param name="requestsPerSecond">1秒あたりのリクエスト数。1～1000の範囲で指定</param>
        /// <param name="exceptionPercentage">例外発生率（パーセンテージ）。0～100の範囲で指定</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        /// <returns>成功メッセージとリクエスト統計（総数、成功数、失敗数）</returns>
        /// <remarks>
        /// 各リクエストは500msの遅延をシミュレートし、実際のバックエンド呼び出しを模倣します
        /// </remarks>
        [HttpGet]
        [Route("probabilisticload/{durationSeconds}/{requestsPerSecond}/{exceptionPercentage}")]
        public async Task<ActionResult<string>> ProbabilisticLoad(
            int durationSeconds,
            int requestsPerSecond,
            int exceptionPercentage,
            CancellationToken cancellationToken)
        {
            if (durationSeconds < 1 || durationSeconds > 1800)
            {
                return BadRequest("durationSeconds must be between 1 and 1800.");
            }

            if (requestsPerSecond < 1 || requestsPerSecond > 1000)
            {
                return BadRequest("requestsPerSecond must be between 1 and 1000.");
            }

            if (exceptionPercentage < 0 || exceptionPercentage > 100)
            {
                return BadRequest("exceptionPercentage must be between 0 and 100.");
            }

            var totalRequests = 0;
            var failureCount = 0;
            var endTime = DateTime.UtcNow.AddSeconds(durationSeconds);

            async Task ExecuteRequestAsync()
            {
                Interlocked.Increment(ref totalRequests);

                try
                {
                    await RunProbabilisticRequestAsync(exceptionPercentage, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Interlocked.Increment(ref failureCount);
                    _logger.LogError(
                        ex,
                        "Probabilistic load request failed (total={Total}, failures={Failures})",
                        totalRequests,
                        failureCount);
                }
            }

            while (DateTime.UtcNow < endTime)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var windowStart = DateTime.UtcNow;
                var requestBatch = new List<Task>(requestsPerSecond);

                for (int i = 0; i < requestsPerSecond; i++)
                {
                    requestBatch.Add(ExecuteRequestAsync());
                }

                await Task.WhenAll(requestBatch);

                var remainingWindow = TimeSpan.FromSeconds(1) - (DateTime.UtcNow - windowStart);
                if (remainingWindow > TimeSpan.Zero)
                {
                    await Task.Delay(remainingWindow, cancellationToken);
                }
            }

            var successCount = totalRequests - failureCount;

            return $"success:probabilisticload (durationSeconds={durationSeconds}, totalRequests={totalRequests}, successes={successCount}, failures={failureCount})";
        }

        /// <summary>
        /// 高CPU使用率シナリオを実行します
        /// 指定された時間、CPUビジーループを実行してCPU使用率を100%近くまで上げます
        /// </summary>
        /// <param name="milliseconds">ビジーループの実行時間（ミリ秒）。100～30000の範囲を推奨</param>
        /// <returns>成功メッセージ</returns>
        /// <remarks>
        /// CPU監視ツールやパフォーマンスプロファイラーのテストに使用します
        /// </remarks>
        [HttpGet]
        [Route("highcpu/{milliseconds}")]
        public ActionResult<string> highcpu(int milliseconds)
        {
            var watch = new Stopwatch();
            watch.Start();

            while (true)
            {
                watch.Stop();
                if (watch.ElapsedMilliseconds > milliseconds)
                    break;
                watch.Start();
            }

            return "success:highcpu";
        }
    

        /// <summary>
        /// Task.Wait()を使用した非推奨のタスク待機パターン
        /// スレッドプールの枯渇を引き起こす可能性がある問題のあるコードパターンを示します
        /// </summary>
        /// <returns>成功メッセージ</returns>
        /// <remarks>
        /// このパターンは推奨されません。Task.Result/Task.Wait()は現在のスレッドをブロックし、
        /// スレッドプール枯渇の原因となります。taskasyncwaitエンドポイントの正しいパターンと比較してください
        /// </remarks>
        [HttpGet]
        [Route("taskwait")]
        public ActionResult<string> TaskWait()
        {
            // Using Task.Wait() or Task.Result causes the current thread to block until the
            // result has been computed. This is the most common cause of threadpool starvation
            // and NOT recommended in your own code unless you know the task is complete and won't
            // need to block.
            Customer c = PretendQueryCustomerFromDbAsync("Dana").Result;
            return "success:taskwait";
        }

        /// <summary>
        /// Thread.Sleep()を使用した非推奨のタスク待機パターン
        /// .NET 6.0以降でもスレッドプール問題を引き起こす可能性がある問題のあるコードパターンを示します
        /// </summary>
        /// <returns>成功メッセージ</returns>
        /// <remarks>
        /// このパターンは推奨されません。スピンループでタスクの完了を待つと、スレッドが無駄に消費されます
        /// taskasyncwaitエンドポイントの正しいパターンと比較してください
        /// </remarks>
        [HttpGet]
        [Route("tasksleepwait")]
        public ActionResult<string> TaskSleepWait()
        {
            // Starting in .NET 6.0 the threadpool can recognize some of the common ways that
            // code blocks on tasks completing and can mitigate it by more quickly
            // scaling up the number of threadpool threads. This example is a less common
            // way to block on a task completing to show what happens when the threadpool
            // doesn't recognize the blocking behavior. This code is NOT recommended.
            Task dbTask = PretendQueryCustomerFromDbAsync("Dana");
            while(!dbTask.IsCompleted)
            {
                Thread.Sleep(10);
            }
            return "success:tasksleepwait";
        }

        /// <summary>
        /// awaitキーワードを使用した推奨されるタスク待機パターン
        /// スレッドをブロックせず、スレッドプールを効率的に使用する正しいパターンを示します
        /// </summary>
        /// <returns>成功メッセージ</returns>
        /// <remarks>
        /// これが推奨される方法です。awaitを使用すると、待機中にスレッドが他の作業を処理でき、
        /// 大量の並行リクエストを効率的に処理できます
        /// </remarks>
        [HttpGet]
        [Route("taskasyncwait")]
        public async Task<ActionResult<string>> TaskAsyncWait()
        {
            // Using the await keyword allows the current thread to service other workitems and
            // when the database lookup Task is complete a thread from the threadpool will resume
            // execution here. This way no thread is blocked and large numbers of requests can
            // run in parallel without blocking
            Customer c = await PretendQueryCustomerFromDbAsync("Dana");
            return "success:taskasyncwait";
        }

        /// <summary>
        /// 確率的負荷シナリオで使用されるリクエストシミュレーション
        /// バックエンド遅延をシミュレートし、指定確率で例外を発生させます
        /// </summary>
        /// <param name="exceptionPercentage">例外発生率（0～100）</param>
        /// <param name="cancellationToken">キャンセルトークン</param>
        private async Task RunProbabilisticRequestAsync(int exceptionPercentage, CancellationToken cancellationToken)
        {
            // Simulate backend latency before deciding whether to fail this request
            await PretendQueryCustomerFromDbAsync(Guid.NewGuid().ToString());
            cancellationToken.ThrowIfCancellationRequested();

            if (exceptionPercentage == 0)
            {
                return;
            }

            if (Random.Shared.Next(0, 100) < exceptionPercentage)
            {
                throw new InvalidOperationException("Probabilistic backend failure triggered.");
            }
        }


        /// <summary>
        /// データベースクエリをシミュレートするヘルパーメソッド
        /// 実際のデータベースの代わりに固定遅延を使用します
        /// </summary>
        /// <param name="customerId">顧客ID</param>
        /// <returns>顧客オブジェクト</returns>
        /// <remarks>
        /// 500msの固定遅延で実際のデータベースクエリのレイテンシをシミュレートします
        /// </remarks>
        private async Task<Customer> PretendQueryCustomerFromDbAsync(string customerId)
        {
            // To keep the demo app easy to set up and performing consistently we have replaced a real database query
            // with a fixed delay of 500ms. The impact on application performance should be similar to using a real
            // database that had similar latency.
            await Task.Delay(500);
            return new Customer(customerId);
        }

        /// <summary>
        /// メモリリークをシミュレートする内部クラス
        /// オブジェクトを永続的に保持してメモリリークを再現します
        /// </summary>
        private sealed class MemoryLeakSimulator
        {
            private readonly object _syncRoot = new();
            private readonly List<Processor> _retainedProcessors = new();

            /// <summary>
            /// 指定されたサイズのメモリを確保し、解放せずに保持します
            /// </summary>
            /// <param name="kilobytes">確保するメモリサイズ（KB）</param>
            public void Allocate(int kilobytes)
            {
                var processor = new Processor();
                var iterations = Math.Max(1, (kilobytes * 1000) / 100);
                for (var i = 0; i < iterations; i++)
                {
                    processor.ProcessTransaction(new Customer(Guid.NewGuid().ToString()));
                }

                lock (_syncRoot)
                {
                    _retainedProcessors.Add(processor);
                }
            }
        }

    }

    /// <summary>
    /// テストデータ用の顧客クラス
    /// メモリスパイクやリークシナリオで使用されます
    /// </summary>
    internal class Customer
    {
        private readonly string id;

        public Customer(string id)
        {
            this.id = id;
        }
    }

    /// <summary>
    /// テストデータ用の顧客キャッシュクラス
    /// メモリリークシミュレーションで顧客オブジェクトを蓄積します
    /// </summary>
    internal class CustomerCache
    {
        private List<Customer> cache = new List<Customer>();

        public void AddCustomer(Customer c)
        {
            cache.Add(c);
        }
    }

    /// <summary>
    /// テストデータ用のプロセッサクラス
    /// 顧客トランザクションを処理し、メモリを消費します
    /// </summary>
    internal class Processor
    {
        private CustomerCache cache = new CustomerCache();

        public void ProcessTransaction(Customer customer)
        {
            cache.AddCustomer(customer);
        }
    }
}
