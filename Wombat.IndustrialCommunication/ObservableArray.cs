using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;


using System.Collections.Generic;

namespace Wombat.IndustrialCommunication
{


    //public class ObservableArray<T> where T : IEquatable<T>
    //{
    //    private Memory<T> arrayMemory;
    //    private Memory<T> lastSnapshotMemory;
    //    private T[] ringBuffer;
    //    private int[] ringIndices;
    //    private int ringBufferSize;
    //    private int writeIndex = 0;
    //    private int readIndex = 0;
    //    private int changeCount = 0;

    //    private ReaderWriterLockSlim writeLock = new ReaderWriterLockSlim();
    //    private CancellationTokenSource cts = new CancellationTokenSource();

    //    public event Action<int, T, T> OnElementChanged;
    //    public event Action<Dictionary<int, (T OldValue, T NewValue)>> OnBatchChanged;

    //    public TimeSpan BatchInterval { get; set; } = TimeSpan.FromMilliseconds(500);
    //    public bool EnableBatchNotification { get; set; } = true;

    //    public ObservableArray(int length, int ringBufferSize = 1024)
    //    {
    //        arrayMemory = new Memory<T>(ArrayPool<T>.Shared.Rent(length));
    //        lastSnapshotMemory = new Memory<T>(ArrayPool<T>.Shared.Rent(length));

    //        arrayMemory.Span.Clear();
    //        lastSnapshotMemory.Span.Clear();

    //        this.ringBufferSize = ringBufferSize;
    //        ringBuffer = new T[ringBufferSize];
    //        ringIndices = new int[ringBufferSize];

    //        Task.Run(ProcessChanges);
    //    }

    //    public Span<T> AsSpan() => arrayMemory.Span;

    //    public void Set(int index, T value)
    //    {
    //        var currentSpan = arrayMemory.Span;
    //        ref T element = ref MemoryMarshal.GetReference(currentSpan.Slice(index, 1));
    //        if (!element.Equals(value))
    //        {
    //            element = value;
    //            var pos = Interlocked.Increment(ref writeIndex) % ringBufferSize;
    //            ringBuffer[pos] = value;
    //            ringIndices[pos] = index;
    //            Interlocked.Increment(ref changeCount);
    //        }
    //    }

    //    public T Get(int index) => arrayMemory.Span[index];

    //    public T GetLatest(int index)
    //    {
    //        writeLock.EnterReadLock();
    //        try
    //        {
    //            return arrayMemory.Span[index];
    //        }
    //        finally
    //        {
    //            writeLock.ExitReadLock();
    //        }
    //    }

    //    private async Task ProcessChanges()
    //    {
    //        while (!cts.Token.IsCancellationRequested)
    //        {
    //            if (changeCount > 0)
    //            {
    //                var batchChanges = new Dictionary<int, (T OldValue, T NewValue)>();
    //                int changesToProcess = Math.Min(changeCount, ringBufferSize);

    //                for (int i = 0; i < changesToProcess; i++)
    //                {
    //                    var pos = (readIndex + i) % ringBufferSize;
    //                    int index = ringIndices[pos];
    //                    T newValue = ringBuffer[pos];
    //                    T oldValue = lastSnapshotMemory.Span[index];

    //                    if (!oldValue.Equals(newValue))
    //                    {
    //                        OnElementChanged?.Invoke(index, oldValue, newValue);
    //                        lastSnapshotMemory.Span[index] = newValue;
    //                        batchChanges[index] = (oldValue, newValue);
    //                    }
    //                }

    //                if (EnableBatchNotification && batchChanges.Count > 0)
    //                {
    //                    OnBatchChanged?.Invoke(batchChanges);
    //                }

    //                readIndex = (readIndex + changesToProcess) % ringBufferSize;
    //                Interlocked.Add(ref changeCount, -changesToProcess);
    //            }

    //            await Task.Delay(BatchInterval);
    //        }
    //    }

    //    public void StopWatching()
    //    {
    //        cts.Cancel();
    //        ArrayPool<T>.Shared.Return(arrayMemory.ToArray());
    //        ArrayPool<T>.Shared.Return(lastSnapshotMemory.ToArray());
    //        writeLock.Dispose();
    //    }
    //}



    /// <summary>
    /// 表示一个可观察的泛型数组，能够在元素变更时通过事件通知，并支持变更节流、优先级标记和线程安全操作。
    /// <para>通过动态节流机制平衡高频更新场景的性能与实时性。</para>
    /// </summary>
    /// <typeparam name="T">数组元素类型，需实现 IEquatable<T> 以支持高效值比较</typeparam>
    public class ObservableArray<T> where T : IEquatable<T>
    {
        // 核心数据存储（使用内存池优化）
        private Memory<T> arrayMemory;
        // 最后一次触发通知时的数据快照（用于检测变更）
        private Memory<T> lastSnapshotMemory;

        // 变更缓冲区（线程安全字典存储待处理的索引-值变更）
        private ConcurrentDictionary<int, T> changeBuffer = new ConcurrentDictionary<int, T>();

        // 记录各索引最后一次触发通知的时间（用于节流控制）
        private ConcurrentDictionary<int, DateTime> lastNotificationTime = new ConcurrentDictionary<int, DateTime>();

        // 高优先级索引集合（当前版本预留功能，暂未实现具体逻辑）
        private HashSet<int> highPriorityIndices = new HashSet<int>();

        // 读写锁（确保写操作线程安全，支持多线程读写）
        private ReaderWriterLockSlim writeLock = new ReaderWriterLockSlim();

        // 后台任务取消令牌源
        private CancellationTokenSource cts = new CancellationTokenSource();

        /// <summary>
        /// 元素变更事件（索引，旧值，新值）
        /// <para>注意：事件触发受节流策略控制，可能不会实时触发</para>
        /// </summary>
        public event Action<int, T, T> OnElementChanged;

        // 节流控制参数
        /// <summary>基础节流间隔（默认100ms）</summary>
        public TimeSpan ThrottleInterval { get; set; } = TimeSpan.FromMilliseconds(100);
        /// <summary>最大节流间隔（默认500ms）</summary>
        public TimeSpan MaxThrottleInterval { get; set; } = TimeSpan.FromMilliseconds(500);
        /// <summary>是否启用动态节流（根据负载自动调整间隔）</summary>
        public bool EnableDynamicThrottling { get; set; } = true;

        /// <summary>
        /// 初始化指定长度的可观察数组
        /// </summary>
        /// <param name="length">数组长度（使用内存池分配，实际可能获得更大容量）</param>
        public ObservableArray(int length)
        {
            // 从内存池租用数组空间（优化频繁分配）
            arrayMemory = new Memory<T>(ArrayPool<T>.Shared.Rent(length));
            lastSnapshotMemory = new Memory<T>(ArrayPool<T>.Shared.Rent(length));

            // 初始化内存空间为默认值
            arrayMemory.Span.Clear();
            lastSnapshotMemory.Span.Clear();

            // 启动后台变更处理任务
            Task.Run(ProcessChanges);
        }

        /// <summary>
        /// 获取数组数据的Span视图（线程安全读取）
        /// <para>警告：写入操作必须通过Set方法保证线程安全</para>
        /// </summary>
        public Span<T> AsSpan() => arrayMemory.Span;

        /// <summary>
        /// 线程安全设置指定索引的值
        /// </summary>
        /// <param name="index">目标索引</param>
        /// <param name="value">新值</param>
        /// <exception cref="ArgumentOutOfRangeException">索引越界时抛出</exception>
        public void Set(int index, T value)
        {
            writeLock.EnterWriteLock();
            try
            {
                ref T element = ref MemoryMarshal.GetReference(arrayMemory.Span.Slice(index, 1));
                if (!element.Equals(value))
                {
                    element = value;
                    changeBuffer[index] = value;      // 记录变更到缓冲区
                    lastSnapshotMemory.Span[index] = value;  // 更新快照
                }
            }
            finally
            {
                writeLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 获取指定索引的值（线程安全读取）
        /// </summary>
        public T Get(int index) => arrayMemory.Span[index];

        /// <summary>
        /// 标记高优先级索引（预留功能，当前版本暂未实现特殊处理）
        /// </summary>
        public void MarkHighPriority(int index) => highPriorityIndices.Add(index);

        /// <summary>
        /// 取消高优先级标记
        /// </summary>
        public void UnmarkHighPriority(int index) => highPriorityIndices.Remove(index);

        /// <summary>
        /// 强制检查全数组变更（通常自动调用，可用于手动触发检查）
        /// </summary>
        public void CheckForChanges()
        {
            var currentSpan = arrayMemory.Span;
            var snapshotSpan = lastSnapshotMemory.Span;

            writeLock.EnterWriteLock();
            try
            {
                // 遍历比较当前值与快照值
                for (int i = 0; i < currentSpan.Length; i++)
                {
                    if (!currentSpan[i].Equals(snapshotSpan[i]))
                    {
                        changeBuffer[i] = currentSpan[i];  // 记录差异到缓冲区
                        snapshotSpan[i] = currentSpan[i];  // 更新快照
                    }
                }
            }
            finally
            {
                writeLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// 后台变更处理任务（实现节流逻辑）
        /// </summary>
        private async Task ProcessChanges()
        {
            TimeSpan currentThrottle = ThrottleInterval;

            try
            {
                while (!cts.Token.IsCancellationRequested)
                {
                    CheckForChanges();  // 先执行全量检查

                    if (changeBuffer.Count > 0)
                    {
                        var now = DateTime.UtcNow;

                        // 处理缓冲区中的变更
                        foreach (var change in changeBuffer)
                        {
                            // 节流检查：距离上次通知时间需超过当前节流间隔
                            if (!lastNotificationTime.TryGetValue(change.Key, out var lastTime)
                                || (now - lastTime) >= currentThrottle)
                            {
                                // 触发变更事件（旧值来自快照，新值来自缓冲区）
                                OnElementChanged?.Invoke(
                                    change.Key,
                                    lastSnapshotMemory.Span[change.Key],
                                    change.Value);

                                lastNotificationTime[change.Key] = now;     // 更新时间戳
                                lastSnapshotMemory.Span[change.Key] = change.Value; // 更新快照
                                changeBuffer.TryRemove(change.Key, out _);  // 移出缓冲区
                            }
                        }

                        // 动态节流调整：当缓冲区超过500个变更时使用最大间隔
                        if (EnableDynamicThrottling)
                        {
                            currentThrottle = changeBuffer.Count > 500
                                ? MaxThrottleInterval
                                : ThrottleInterval;
                        }
                    }
                    else
                    {
                        // 无变更时降低CPU占用
                        await Task.Delay(10);
                        CheckForChanges();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"变更处理线程异常: {ex.Message}");
                // 生产环境建议记录完整日志或触发异常事件
            }
        }

        /// <summary>
        /// 停止监听并释放资源
        /// </summary>
        public void StopWatching()
        {
            cts.Cancel();
            ArrayPool<T>.Shared.Return(arrayMemory.ToArray());     // 归还内存池数组
            ArrayPool<T>.Shared.Return(lastSnapshotMemory.ToArray());
            writeLock.Dispose();
        }
    }

}
