using System;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Wombat.IndustrialCommunicationTestProject.Helper
{
    /// <summary>
    /// 测试日志工厂，用于在测试中创建日志记录器
    /// </summary>
    public static class TestLoggerFactory
    {
        /// <summary>
        /// 从测试输出帮助器创建日志记录器
        /// </summary>
        /// <typeparam name="T">日志分类类型</typeparam>
        /// <param name="output">测试输出帮助器</param>
        /// <returns>日志记录器</returns>
        public static ILogger<T> CreateLogger<T>(ITestOutputHelper output)
        {
            return new XUnitLogger<T>(output);
        }
        
        /// <summary>
        /// 从测试输出帮助器创建非泛型日志记录器
        /// </summary>
        /// <param name="output">测试输出帮助器</param>
        /// <param name="categoryName">日志分类名称</param>
        /// <returns>日志记录器</returns>
        public static ILogger CreateLogger(ITestOutputHelper output, string categoryName = "Test")
        {
            return new XUnitLogger(output, categoryName);
        }
        
        /// <summary>
        /// 创建控制台日志记录器（用于非测试环境）
        /// </summary>
        /// <typeparam name="T">日志分类类型</typeparam>
        /// <returns>日志记录器</returns>
        public static ILogger<T> CreateConsoleLogger<T>()
        {
            return new ConsoleLogger<T>();
        }
    }
    
    /// <summary>
    /// XUnit测试日志记录器
    /// </summary>
    public class XUnitLogger : ILogger
    {
        private readonly ITestOutputHelper _output;
        private readonly string _categoryName;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="output">测试输出帮助器</param>
        /// <param name="categoryName">日志分类名称</param>
        public XUnitLogger(ITestOutputHelper output, string categoryName)
        {
            _output = output;
            _categoryName = categoryName;
        }
        
        /// <summary>
        /// 创建日志作用域
        /// </summary>
        public IDisposable BeginScope<TState>(TState state) => null;
        
        /// <summary>
        /// 判断日志级别是否启用
        /// </summary>
        public bool IsEnabled(LogLevel logLevel) => true;
        
        /// <summary>
        /// 记录日志
        /// </summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            try
            {
                _output.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {formatter(state, exception)}");
                if (exception != null)
                    _output.WriteLine($"Exception: {exception}");
            }
            catch
            {
                // 忽略日志记录异常，防止测试崩溃
            }
        }
    }
    
    /// <summary>
    /// XUnit测试日志记录器（泛型版本）
    /// </summary>
    /// <typeparam name="T">日志分类类型</typeparam>
    public class XUnitLogger<T> : XUnitLogger, ILogger<T>
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="output">测试输出帮助器</param>
        public XUnitLogger(ITestOutputHelper output) 
            : base(output, typeof(T).Name)
        {
        }
    }
    
    /// <summary>
    /// 控制台日志记录器
    /// </summary>
    public class ConsoleLogger : ILogger
    {
        private readonly string _categoryName;
        
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="categoryName">日志分类名称</param>
        public ConsoleLogger(string categoryName)
        {
            _categoryName = categoryName;
        }
        
        /// <summary>
        /// 创建日志作用域
        /// </summary>
        public IDisposable BeginScope<TState>(TState state) => null;
        
        /// <summary>
        /// 判断日志级别是否启用
        /// </summary>
        public bool IsEnabled(LogLevel logLevel) => true;
        
        /// <summary>
        /// 记录日志
        /// </summary>
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var originalColor = Console.ForegroundColor;
            
            try
            {
                // 根据日志级别设置颜色
                switch (logLevel)
                {
                    case LogLevel.Critical:
                    case LogLevel.Error:
                        Console.ForegroundColor = ConsoleColor.Red;
                        break;
                    case LogLevel.Warning:
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        break;
                    case LogLevel.Information:
                        Console.ForegroundColor = ConsoleColor.White;
                        break;
                    case LogLevel.Debug:
                    case LogLevel.Trace:
                        Console.ForegroundColor = ConsoleColor.Gray;
                        break;
                }
                
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {formatter(state, exception)}");
                
                if (exception != null)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"Exception: {exception}");
                }
            }
            finally
            {
                Console.ForegroundColor = originalColor;
            }
        }
    }
    
    /// <summary>
    /// 控制台日志记录器（泛型版本）
    /// </summary>
    /// <typeparam name="T">日志分类类型</typeparam>
    public class ConsoleLogger<T> : ConsoleLogger, ILogger<T>
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        public ConsoleLogger() 
            : base(typeof(T).Name)
        {
        }
    }
} 