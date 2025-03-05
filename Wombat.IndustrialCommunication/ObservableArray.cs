using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;


using System.Collections.Generic;

namespace Wombat.IndustrialCommunication
{


    public class ObservableArray<T> where T : IEquatable<T>
    {
        private Memory<T> memory;
        private T[] lastSnapshot;
        private ConcurrentQueue<(int index, T oldValue, T newValue)> changeQueue = new ConcurrentQueue<(int index, T oldValue, T newValue)>();
        private CancellationTokenSource cts = new CancellationTokenSource();

        public event Action<int, T, T> OnElementChanged;

        public ObservableArray(int length)
        {
            memory = new Memory<T>(new T[length]);
            lastSnapshot = new T[length];

            // 启动异步通知任务
            Task.Run(ProcessChanges);
        }

        public Span<T> AsSpan() => memory.Span;

        public void Set(int index, T value)
        {
            if (!memory.Span[index].Equals(value))
            {
                T oldValue = memory.Span[index];
                memory.Span[index] = value;

                // 把变化放入队列（非阻塞）
                changeQueue.Enqueue((index, oldValue, value));
            }
        }

        public void CheckForChanges()
        {
            var currentSpan = memory.Span;
            for (int i = 0; i < currentSpan.Length; i++)
            {
                if (!currentSpan[i].Equals(lastSnapshot[i]))
                {
                    changeQueue.Enqueue((i, lastSnapshot[i], currentSpan[i]));
                    lastSnapshot[i] = currentSpan[i]; // 更新快照
                }
            }
        }

        private async Task ProcessChanges()
        {
            while (!cts.Token.IsCancellationRequested)
            {
                while (changeQueue.TryDequeue(out var change))
                {
                    OnElementChanged?.Invoke(change.index, change.oldValue, change.newValue);
                }
                await Task.Delay(10); // 小延迟，批量处理
            }
        }

        public void StopWatching()
        {
            cts.Cancel();
        }
    }
}
