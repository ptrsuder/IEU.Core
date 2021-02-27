using System.Threading;
using System.Threading.Tasks;
using NvAPIWrapper;
using NvAPIWrapper.GPU;

namespace ImageEnhancingUtility.Core
{
    public class GpuMonitor
    {
        public uint vmemory, vcurMemory;
        public PhysicalGPU gpu;
        public CancellationTokenSource MonitorVramTokenSource;
        Logger Logger;

        public GpuMonitor(Logger logger)
        {
            Logger = logger;
        }

        public void GetVRAM()
        {
            NVIDIA.Initialize();
            var a = PhysicalGPU.GetPhysicalGPUs();
            if (a.Length == 0) return;
            gpu = a[0];
            vmemory = (gpu.MemoryInformation.AvailableDedicatedVideoMemoryInkB / 1000);
            vcurMemory = (gpu.MemoryInformation.CurrentAvailableDedicatedVideoMemoryInkB / 1000);
            Logger.Write($"{gpu.FullName}: {vmemory} MB");
            Logger.Write($"Currently available VRAM: {vcurMemory} MB");
        }
        public void MonitorVramStart(bool VramMonitorEnable, int VramMonitorFrequency)
        {
            MonitorVramTokenSource = new CancellationTokenSource();

            if (VramMonitorEnable)
            {
                CancellationToken ct = MonitorVramTokenSource.Token;

                NVIDIA.Initialize();
                var a = PhysicalGPU.GetPhysicalGPUs();
                var gpu = a[0];

                var task = Task.Run(() =>
                {
                    ct.ThrowIfCancellationRequested();

                    while (true)
                    {
                        if (ct.IsCancellationRequested)
                        {
                            break;
                            // Clean up here, then...
                            //ct.ThrowIfCancellationRequested();
                        }
                        var usage = (gpu.MemoryInformation.AvailableDedicatedVideoMemoryInkB - gpu.MemoryInformation.CurrentAvailableDedicatedVideoMemoryInkB) / 1000;
                        Logger.Write($"Using {usage} MB");
                        Thread.Sleep(VramMonitorFrequency);
                    }
                }, MonitorVramTokenSource.Token);
            }
        }

    }

}
