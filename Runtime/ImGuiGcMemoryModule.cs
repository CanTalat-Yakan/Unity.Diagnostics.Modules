using System;
using ImGuiNET;
using UnityEngine;

#if UNITY_2020_2_OR_NEWER
using Unity.Profiling;
#endif

namespace UnityEssentials
{
    internal sealed class ImGuiGcMemoryModule : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private string _windowTitle = "GC / Memory";

        private const int HistorySize = 240;

        private readonly float[] _managedMbHistory = new float[HistorySize];
        private readonly float[] _allocKbHistory = new float[HistorySize];
        private readonly float[] _scratch = new float[HistorySize];
        private int _cursor;
        private int _count;

        private long _lastManagedBytes;
        private float _lastManagedMb;
        private float _allocKbThisFrame;

        private int _lastGen0;
        private int _lastGen1;
        private int _lastGen2;
        private int _deltaGen0;
        private int _deltaGen1;
        private int _deltaGen2;

#if UNITY_2020_2_OR_NEWER
        private ProfilerRecorder _gcAllocRecorder;
        private ProfilerRecorder _gcUsedRecorder;
        private ProfilerRecorder _gcReservedRecorder;
#endif

        private bool _recordersReady;
        private float _nextTextRefresh;
        private string _managedText = "";
        private string _heapText = "";
        private string _allocText = "";

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
            Array.Clear(_managedMbHistory, 0, _managedMbHistory.Length);
            Array.Clear(_allocKbHistory, 0, _allocKbHistory.Length);
            Array.Clear(_scratch, 0, _scratch.Length);
            _cursor = 0;
            _count = 0;

            _lastManagedBytes = 0;
            _lastManagedMb = 0;
            _allocKbThisFrame = 0;

            _lastGen0 = 0;
            _lastGen1 = 0;
            _lastGen2 = 0;
            _deltaGen0 = 0;
            _deltaGen1 = 0;
            _deltaGen2 = 0;

            _recordersReady = false;
            _nextTextRefresh = 0;
            _managedText = "";
            _heapText = "";
            _allocText = "";

            DisposeRecorders();
        }

        private void DisposeRecorders()
        {
#if UNITY_2020_2_OR_NEWER
            if (_gcAllocRecorder.Valid) _gcAllocRecorder.Dispose();
            if (_gcUsedRecorder.Valid) _gcUsedRecorder.Dispose();
            if (_gcReservedRecorder.Valid) _gcReservedRecorder.Dispose();
#endif
        }

        private void EnsureRecorders()
        {
#if UNITY_2020_2_OR_NEWER
            if (_recordersReady)
                return;

            // These counters exist on most recent Unity versions; if unavailable, we gracefully fall back.
            try { _gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 15); } catch { }
            try { _gcUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory", 15); } catch { }
            try { _gcReservedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory", 15); } catch { }

            _recordersReady = true;
#endif
        }

        private void DrawImGui()
        {
            EnsureRecorders();

            if (!ImGui.Begin(_windowTitle))
            {
                ImGui.End();
                return;
            }

            var managedBytes = GC.GetTotalMemory(false);
            var managedMb = managedBytes / (1024f * 1024f);

            // Heuristic alloc estimate from managed bytes (works everywhere but noisy).
            var allocFromDeltaKb = 0f;
            if (_lastManagedBytes > 0 && managedBytes >= _lastManagedBytes)
                allocFromDeltaKb = (managedBytes - _lastManagedBytes) / 1024f;

#if UNITY_2020_2_OR_NEWER
            if (_gcAllocRecorder.Valid)
            {
                // GC.Alloc is bytes allocated since last frame (approx). Use LastValue.
                _allocKbThisFrame = _gcAllocRecorder.LastValue / 1024f;
            }
            else
#endif
            {
                _allocKbThisFrame = allocFromDeltaKb;
            }

            _lastManagedBytes = managedBytes;
            _lastManagedMb = managedMb;

            // Gen counts (more direct than memory-drop detection)
            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            _deltaGen0 = gen0 - _lastGen0;
            _deltaGen1 = gen1 - _lastGen1;
            _deltaGen2 = gen2 - _lastGen2;

            _lastGen0 = gen0;
            _lastGen1 = gen1;
            _lastGen2 = gen2;

            // history
            _managedMbHistory[_cursor] = managedMb;
            _allocKbHistory[_cursor] = _allocKbThisFrame;
            _cursor = (_cursor + 1) % HistorySize;
            _count = Mathf.Min(HistorySize, _count + 1);

            // Throttle string formatting to reduce GC from interpolation.
            if (Time.unscaledTime >= _nextTextRefresh)
            {
                _nextTextRefresh = Time.unscaledTime + 0.25f;

                _managedText = $"Managed Used: {managedMb:0.0} MB";

#if UNITY_2020_2_OR_NEWER
                if (_gcUsedRecorder.Valid)
                    _heapText = $"GC Used: {_gcUsedRecorder.LastValue / (1024f * 1024f):0.0} MB";
                else
#endif
                    _heapText = "GC Used: N/A";

#if UNITY_2020_2_OR_NEWER
                if (_gcReservedRecorder.Valid)
                    _heapText += $" | GC Reserved: {_gcReservedRecorder.LastValue / (1024f * 1024f):0.0} MB";
                else
                    _heapText += " | GC Reserved: N/A";
#endif

                _allocText = $"GC Alloc: {_allocKbThisFrame:0} KB/frame";
            }

            ImGui.TextUnformatted("Managed memory");
            ImGui.Separator();

            ImGui.TextUnformatted(_managedText);
            ImGui.TextUnformatted(_heapText);
            ImGui.TextUnformatted(_allocText);

            ImGui.TextUnformatted($"Collections (Δ): Gen0 {_deltaGen0}, Gen1 {_deltaGen1}, Gen2 {_deltaGen2}");

            ImGui.Spacing();

            var n = _count;
            for (var i = 0; i < n; i++)
            {
                var idx = (_cursor - n + i);
                if (idx < 0) idx += HistorySize;
                _scratch[i] = _managedMbHistory[idx];
            }

            if (n > 0)
                ImGui.PlotLines("Managed MB", ref _scratch[0], n, 0, null, 0f, Math.Max(64f, _lastManagedMb * 1.2f), new System.Numerics.Vector2(0, 80));

            for (var i = 0; i < n; i++)
            {
                var idx = (_cursor - n + i);
                if (idx < 0) idx += HistorySize;
                _scratch[i] = _allocKbHistory[idx];
            }

            if (n > 0)
                ImGui.PlotLines("Alloc KB/frame", ref _scratch[0], n, 0, null, 0f, 256f, new System.Numerics.Vector2(0, 80));

            ImGui.Spacing();
            ImGui.TextDisabled("Notes: GC.Alloc uses Profiler counters when available; otherwise it’s estimated from managed memory deltas.");

            ImGui.End();
        }
    }
}
