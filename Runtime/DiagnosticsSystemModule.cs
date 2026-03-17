using System.Globalization;
using Unity.Profiling;
using UnityEngine;

namespace UnityEssentials
{
    [MonitorDock(MonitorCorner.TopRight)]
    internal sealed class DiagnosticsSystemModule : MonoBehaviour
    {
        [Monitor("OS", Group = "System")]
        private string MonitoredOs => _os;

        [Monitor("OS Family", Group = "System")]
        private string MonitoredOsFamily => _osFamily;

        [Monitor("Device", Group = "System")]
        private string MonitoredDevice => _device;

        [Monitor("Battery", Group = "System")]
        private string MonitoredBattery => _battery;


        [Monitor("CPU", Group = "CPU")]
        private string MonitoredCpu => _cpu;

        [Monitor("CPU Cores", Group = "CPU")]
        private string MonitoredCpuCores => _cpuCores;

        [Monitor("CPU Freq", Group = "CPU")]
        private string MonitoredCpuFreq => _cpuFreq;


        [Monitor("GPU", Group = "GPU")]
        private string MonitoredGpu => _gpu;

        [Monitor("GPU API", Group = "GPU")]
        private string MonitoredGpuApi => _gpuApi;

        [Monitor("VRAM", Group = "GPU")]
        private string MonitoredVram => _vram;

        [Monitor("GPU MT", Group = "GPU")]
        private string MonitoredGpuMt => _gpuMt;


        [Monitor("RAM", Group = "RAM")]
        private string MonitoredRam => _ram;

        [Monitor("Total Reserved", Group = "RAM")]
        private string MonitoredTotalReserved => _totalReservedText;

        [Monitor("Total Used", Group = "RAM")]
        private string MonitoredTotalUsed => _totalUsedText;

        [Monitor("System Used", Group = "RAM")]
        private string MonitoredSystemUsed => _systemUsedText;

        private bool _cached;
        
        private string _os = "";
        private string _osFamily = "";
        private string _device = "";
        private string _cpu = "";
        private string _cpuCores = "";
        private string _cpuFreq = "";
        private string _ram = "";
        private string _gpu = "";
        private string _gpuApi = "";
        private string _vram = "";
        private string _gpuMt = "";
        private string _battery = "";

        private ProfilerRecorder _totalReservedRecorder;
        private ProfilerRecorder _totalUsedRecorder;
        private ProfilerRecorder _systemUsedRecorder;

        private float _nextMemRefresh;
        private string _totalReservedText = "";
        private string _totalUsedText = "";
        private string _systemUsedText = "";

        private void OnEnable() => ResetState();

        private void OnDisable() => DisposeRecorders();

        private void OnDestroy() => DisposeRecorders();

        private void Update()
        {
            EnsureCached();

            if (Time.unscaledTime >= _nextMemRefresh)
            {
                _nextMemRefresh = Time.unscaledTime + 0.5f;

                _totalReservedText = _totalReservedRecorder.Valid
                    ? BytesToMb(_totalReservedRecorder.LastValue)
                    : "N/A";
                _totalUsedText = _totalUsedRecorder.Valid
                    ? BytesToMb(_totalUsedRecorder.LastValue)
                    : "N/A";
                _systemUsedText = _systemUsedRecorder.Valid
                    ? BytesToMb(_systemUsedRecorder.LastValue)
                    : "N/A";
            }
        }

        private void ResetState()
        {
            _cached = false;
            DisposeRecorders();
        }

        private void DisposeRecorders()
        {
            if (_totalReservedRecorder.Valid) _totalReservedRecorder.Dispose();
            if (_totalUsedRecorder.Valid) _totalUsedRecorder.Dispose();
            if (_systemUsedRecorder.Valid) _systemUsedRecorder.Dispose();
        }

        private void EnsureCached()
        {
            if (_cached)
                return;

            _os = SystemInfo.operatingSystem;
            _osFamily = SystemInfo.operatingSystemFamily.ToString();

            _device = $"{SystemInfo.deviceType} {SystemInfo.deviceModel} ({SystemInfo.deviceName})";

            _cpu = SystemInfo.processorType;
            _cpuCores = SystemInfo.processorCount.ToString(CultureInfo.InvariantCulture);
            _cpuFreq = (SystemInfo.processorFrequency * 0.001f).ToString("0.00", CultureInfo.InvariantCulture) + " GHz";

            _ram = SystemInfo.systemMemorySize.ToString("N0", CultureInfo.InvariantCulture) + " MB";

            _gpu = SystemInfo.graphicsDeviceName;
            _gpuApi = SystemInfo.graphicsDeviceType.ToString();
            _vram = SystemInfo.graphicsMemorySize.ToString("N0", CultureInfo.InvariantCulture) + " MB";
            _gpuMt = SystemInfo.graphicsMultiThreaded.ToString();

            _battery = $"{SystemInfo.batteryLevel * 100f:F0}% ({SystemInfo.batteryStatus})";

            try { _totalReservedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory"); } catch { }
            try { _totalUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory"); } catch { }
            try { _systemUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory"); } catch { }

            _cached = true;
        }

        private static string BytesToMb(long bytes) =>
            (bytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }
}