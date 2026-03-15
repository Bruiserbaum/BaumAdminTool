using BaumAdminTool.Models;

namespace BaumAdminTool.Services;

internal sealed class ProcessService : IDisposable
{
    private Dictionary<int, (DateTime time, TimeSpan cpu)> _prev = new();
    private DateTime _prevTime = DateTime.MinValue;

    public List<ProcessEntry> Sample(int count = 40)
    {
        var now     = DateTime.Now;
        var procs   = System.Diagnostics.Process.GetProcesses();
        var entries = new List<ProcessEntry>(procs.Length);
        var next    = new Dictionary<int, (DateTime, TimeSpan)>(procs.Length);

        double elapsed = _prevTime == DateTime.MinValue ? 0
            : (now - _prevTime).TotalSeconds;

        foreach (var p in procs)
        {
            try
            {
                var cpu = p.TotalProcessorTime;
                next[p.Id] = (now, cpu);

                double cpuPct = 0;
                if (elapsed > 0 && _prev.TryGetValue(p.Id, out var prev))
                {
                    var delta = (cpu - prev.cpu).TotalSeconds;
                    cpuPct = Math.Clamp(delta / (elapsed * Environment.ProcessorCount) * 100, 0, 100);
                }

                entries.Add(new ProcessEntry(p.Id, p.ProcessName, cpuPct, p.WorkingSet64));
            }
            catch { }
            finally { p.Dispose(); }
        }

        _prev     = next;
        _prevTime = now;

        return entries
            .OrderByDescending(e => e.CpuPercent)
            .ThenByDescending(e => e.MemoryBytes)
            .Take(count)
            .ToList();
    }

    public void Dispose() { }
}
