using System.Globalization;
using ImGuiNET;
using UnityEngine;

#if UNITY_2020_2_OR_NEWER
using Unity.Profiling;
#endif

namespace UnityEssentials
{
    internal sealed class ImGuiSystemModule : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private string _windowTitle = "System";

        private bool _cached;

        private string _operatingSystem;
        private string _operatingSystemFamily;

        private string _deviceType;
        private string _deviceModel;
        private string _deviceName;

        private string _processorType;
        private string _processorFrequency;
        private string _processorCount;

        private string _systemMemory;

        private string _graphicsDeviceName;
        private string _graphicsDeviceType;
        private string _graphicsMemorySize;
        private string _graphicsMultiThreaded;

        private string _batteryLevel;
        private string _batteryStatus;

        private string _dataPath;
        private string _persistentDataPath;
        private string _consoleLogPath;
        private string _streamingAssetsPath;
        private string _temporaryCachePath;

#if UNITY_2020_2_OR_NEWER
        private ProfilerRecorder _totalReservedRecorder;
        private ProfilerRecorder _totalUsedRecorder;
        private ProfilerRecorder _systemUsedRecorder;
#endif

        private void OnEnable() => ResetState();
        
        private void OnDisable() => DisposeRecorders();
        
        private void OnDestroy() => DisposeRecorders();
        
        private void Update()
        {
            if (!_enabled)
                return;
        
            DrawImGui();
        }

        private void ResetState()
        {
            _cached = false;
            DisposeRecorders();
        }

        private void DisposeRecorders()
        {
#if UNITY_2020_2_OR_NEWER
            if (_totalReservedRecorder.Valid) _totalReservedRecorder.Dispose();
            if (_totalUsedRecorder.Valid) _totalUsedRecorder.Dispose();
            if (_systemUsedRecorder.Valid) _systemUsedRecorder.Dispose();
#endif
        }

        private void EnsureCached()
        {
            if (_cached)
                return;

            _operatingSystem = SystemInfo.operatingSystem;
            _operatingSystemFamily = SystemInfo.operatingSystemFamily.ToString();

            _deviceModel = SystemInfo.deviceModel;
            _deviceName = SystemInfo.deviceName;
            _deviceType = SystemInfo.deviceType.ToString();

            _processorType = SystemInfo.processorType;
            _processorCount = SystemInfo.processorCount.ToString(CultureInfo.InvariantCulture);
            _processorFrequency = (SystemInfo.processorFrequency * 0.001f).ToString("0.00", CultureInfo.InvariantCulture) + " GHz";

            _systemMemory = SystemInfo.systemMemorySize.ToString("N0", CultureInfo.InvariantCulture) + " MB";

            _graphicsDeviceName = SystemInfo.graphicsDeviceName;
            _graphicsDeviceType = SystemInfo.graphicsDeviceType.ToString();
            _graphicsMemorySize = SystemInfo.graphicsMemorySize.ToString("N0", CultureInfo.InvariantCulture) + " MB";
            _graphicsMultiThreaded = SystemInfo.graphicsMultiThreaded.ToString();

            _batteryLevel = SystemInfo.batteryLevel.ToString(CultureInfo.InvariantCulture);
            _batteryStatus = SystemInfo.batteryStatus.ToString();

            _dataPath = Application.dataPath;
            _persistentDataPath = Application.persistentDataPath;
            _consoleLogPath = Application.consoleLogPath;
            _streamingAssetsPath = Application.streamingAssetsPath;
            _temporaryCachePath = Application.temporaryCachePath;

#if UNITY_2020_2_OR_NEWER
            // Some counters vary across Unity versions; wrap in try/catch.
            try { _totalReservedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Reserved Memory"); } catch { }
            try { _totalUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory"); } catch { }
            try { _systemUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory"); } catch { }
#endif

            _cached = true;
        }

        private void DrawImGui()
        {
            EnsureCached();

            if (!ImGui.Begin(_windowTitle))
            {
                ImGui.End();
                return;
            }

            if (ImGui.BeginTable("SystemTable", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.RowBg))
            {
                Row("OS", _operatingSystem);
                Row("OS Family", _operatingSystemFamily);
                Row("Device", $"{_deviceType} {_deviceModel} ({_deviceName})");
                Row("CPU", _processorType);
                Row("CPU Cores", _processorCount);
                Row("CPU Freq", _processorFrequency);
                Row("RAM", _systemMemory);
                Row("GPU", _graphicsDeviceName);
                Row("GPU API", _graphicsDeviceType);
                Row("VRAM", _graphicsMemorySize);
                Row("GPU MT", _graphicsMultiThreaded);
                Row("Battery", $"{_batteryLevel} ({_batteryStatus})");

#if UNITY_2020_2_OR_NEWER
                if (_totalReservedRecorder.Valid)
                    Row("Total Reserved", BytesToMb(_totalReservedRecorder.LastValue));
                if (_totalUsedRecorder.Valid)
                    Row("Total Used", BytesToMb(_totalUsedRecorder.LastValue));
                if (_systemUsedRecorder.Valid)
                    Row("System Used", BytesToMb(_systemUsedRecorder.LastValue));
#endif

                ImGui.EndTable();
            }

            ImGui.Spacing();

            if (ImGui.CollapsingHeader("Paths", ImGuiTreeNodeFlags.DefaultOpen))
            {
                ImGui.TextWrapped($"Data: {_dataPath}");
                ImGui.TextWrapped($"Persistent: {_persistentDataPath}");
                ImGui.TextWrapped($"Console Log: {_consoleLogPath}");
                ImGui.TextWrapped($"StreamingAssets: {_streamingAssetsPath}");
                ImGui.TextWrapped($"Temp Cache: {_temporaryCachePath}");
            }

            ImGui.End();
        }

        private static void Row(string key, string value)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.TextUnformatted(key);
            ImGui.TableNextColumn();
            ImGui.TextWrapped(value ?? string.Empty);
        }

        private static string BytesToMb(long bytes) => (bytes / (1024f * 1024f)).ToString("0.0", CultureInfo.InvariantCulture) + " MB";
    }
}
