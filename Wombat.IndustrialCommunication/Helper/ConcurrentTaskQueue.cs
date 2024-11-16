using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;

namespace Wombat.IndustrialCommunication
{
    public class ConcurrentTaskQueue
    {
        private readonly ConcurrentQueue<Func<Task<object>>> _taskQueue = new ConcurrentQueue<Func<Task<object>>>();
        private readonly SemaphoreSlim _semaphore;  // 用于控制并发数量
        private readonly SemaphoreSlim _queueSpaceSemaphore; // 用于控制队列的最大容量
        private bool _isRunning = false;

        public ConcurrentTaskQueue(int maxConcurrency, int maxQueueSize)
        {
            _semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency); // 控制并发任务数
            _queueSpaceSemaphore = new SemaphoreSlim(maxQueueSize, maxQueueSize); // 控制队列的最大容量
        }

        // 向队列中添加任务（支持同步和异步），并返回任务结果
        public async Task<T> EnqueueTask<T>(Func<Task<T>> task, int timeoutMilliseconds)
        {
            await _queueSpaceSemaphore.WaitAsync(); // 等待有空位才能加入任务队列

            _taskQueue.Enqueue(async () =>await ExecuteWithTimeout(task, timeoutMilliseconds));

            if (!_isRunning)
            {
               _= Task.Run(() => ProcessQueue()); // 启动任务处理
            }

            return await task(); // 立即返回执行的任务结果
        }

        // 向队列中添加同步任务，返回任务结果
        public async Task<T> EnqueueSyncTask<T>(Func<Task<T>> task, int timeoutMilliseconds)
        {
            await _queueSpaceSemaphore.WaitAsync(); // 等待有空位才能加入任务队列

            _taskQueue.Enqueue(async () => await ExecuteWithTimeout(task, timeoutMilliseconds));

            if (!_isRunning)
            {
               _= Task.Run(() => ProcessQueue()); // 启动任务处理
            }

            // 返回任务的结果，处理异步任务的返回值
            return await task();
        }

        // 处理队列中的任务
        private async Task ProcessQueue()
        {
            _isRunning = true;

            while (!_taskQueue.IsEmpty)
            {
                if (_taskQueue.TryDequeue(out var task))
                {
                    await _semaphore.WaitAsync(); // 控制并发
                    try
                    {
                        await task();
                    }
                    catch (OperationCanceledException)
                    {
                        Console.WriteLine("Task was canceled due to timeout.");
                    }
                    finally
                    {
                        _semaphore.Release(); // 释放一个并发槽
                        _queueSpaceSemaphore.Release(); // 释放一个队列槽
                    }
                }
            }

            _isRunning = false;
        }

        // 执行任务，并设置超时，返回结果
        private async Task<T> ExecuteWithTimeout<T>(Func<Task<T>> task, int timeoutMilliseconds)
        {
            using (var cts = new CancellationTokenSource())
            {
                var timeoutTask = Task.Delay(timeoutMilliseconds, cts.Token);
                var workTask = task();

                var completedTask = await Task.WhenAny(workTask, timeoutTask);

                if (completedTask == timeoutTask)
                {
                    // 如果超时任务先完成，取消原任务
                    cts.Cancel();
                    throw new OperationCanceledException();
                }

                // 如果任务在超时之前完成，取消超时任务
                cts.Cancel();
                return await workTask;
            }
        }
    }

}
