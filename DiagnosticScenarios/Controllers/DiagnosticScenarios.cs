using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace DiagnosticScenarios.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DiagScenarioController : ControllerBase
    {
        private const int MinMemLeakKilobytes = 1;
        private const int MaxMemLeakKilobytes = 10240;

        private readonly object _deadlockLock1 = new();
        private readonly object _deadlockLock2 = new();
        private static readonly MemoryLeakSimulator _memoryLeakSimulator = new();
        private readonly ILogger<DiagScenarioController> _logger;

        public DiagScenarioController(ILogger<DiagScenarioController> logger)
        {
            _logger = logger;
        }

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

        [HttpGet]
        [Route("exception")]
        public ActionResult<string> exception()
        {
            throw new Exception("bad, bad code");
        }

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


        async Task<Customer> PretendQueryCustomerFromDbAsync(string customerId)
        {
            // To keep the demo app easy to set up and performing consistently we have replaced a real database query
            // with a fixed delay of 500ms. The impact on application performance should be similar to using a real
            // database that had similar latency.
            await Task.Delay(500);
            return new Customer(customerId);
        }

        private sealed class MemoryLeakSimulator
        {
            private readonly object _syncRoot = new();
            private readonly List<Processor> _retainedProcessors = new();

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

    class Customer
    {
        private string id;

        public Customer(string id)
        {
            this.id = id;
        }
    }

    class CustomerCache
    {
        private List<Customer> cache = new List<Customer>();

        public void AddCustomer(Customer c)
        {
            cache.Add(c);
        }
    }

    class Processor
    {
        private CustomerCache cache = new CustomerCache();

        public void ProcessTransaction(Customer customer)
        {
            cache.AddCustomer(customer);
        }
    }
}
