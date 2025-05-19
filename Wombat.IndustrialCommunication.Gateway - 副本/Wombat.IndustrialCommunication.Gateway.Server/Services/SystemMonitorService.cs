using System.Diagnostics;
using System.Net;
using System.Runtime.InteropServices;

namespace Wombat.IndustrialCommunication.Gateway.Server.Services;

public class SystemMonitorService
{
    private DateTime _lastCpuTime = DateTime.MinValue;
    private TimeSpan _lastTotalProcessorTime = TimeSpan.Zero;

    public double GetCpuUsage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "cpu get loadpercentage",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split('\n');
            if (lines.Length > 1)
            {
                var cpuLine = lines[1].Trim();
                if (double.TryParse(cpuLine, out double cpuUsage))
                {
                    return Math.Round(cpuUsage, 2);
                }
            }
            return 0;
        }
        else
        {
            // Linux/Mac 使用 top 命令获取 CPU 使用率
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "top",
                    Arguments = "-bn1",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            // 解析 top 命令输出获取 CPU 使用率
            var cpuLine = output.Split('\n').FirstOrDefault(l => l.Contains("Cpu(s)"));
            if (cpuLine != null)
            {
                var cpuUsage = cpuLine.Split(',').FirstOrDefault()?.Split(':').LastOrDefault()?.Trim();
                if (double.TryParse(cpuUsage, out double usage))
                {
                    return Math.Round(usage, 2);
                }
            }
            return 0;
        }
    }

    public double GetMemoryUsage()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "wmic",
                    Arguments = "OS get FreePhysicalMemory,TotalVisibleMemorySize /Value",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            var lines = output.Split('\n');
            var freeMemory = 0L;
            var totalMemory = 0L;

            foreach (var line in lines)
            {
                if (line.StartsWith("FreePhysicalMemory="))
                {
                    long.TryParse(line.Split('=')[1], out freeMemory);
                }
                else if (line.StartsWith("TotalVisibleMemorySize="))
                {
                    long.TryParse(line.Split('=')[1], out totalMemory);
                }
            }

            if (totalMemory > 0)
            {
                var usedMemory = totalMemory - freeMemory;
                return Math.Round((double)usedMemory / totalMemory * 100, 2);
            }
            return 0;
        }
        else
        {
            // Linux/Mac 使用 free 命令获取内存使用率
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "free",
                    Arguments = "-m",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            process.Start();
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            
            // 解析 free 命令输出获取内存使用率
            var lines = output.Split('\n');
            if (lines.Length > 1)
            {
                var memoryLine = lines[1].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (memoryLine.Length >= 3)
                {
                    if (double.TryParse(memoryLine[1], out double total) && 
                        double.TryParse(memoryLine[2], out double used))
                    {
                        return Math.Round((used / total) * 100, 2);
                    }
                }
            }
            return 0;
        }
    }

    public string GetClientIP()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());
        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return "Unknown";
    }
} 