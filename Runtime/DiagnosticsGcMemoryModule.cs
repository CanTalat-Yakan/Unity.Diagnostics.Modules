using System;
using Unity.Profiling;
using UnityEngine;

namespace UnityEssentials
{
    [MonitorDock(MonitorCorner.BottomLeft)]
    internal sealed class DiagnosticsGcMemoryModule : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;

        [Monitor("Managed", Group = "GC")]
        private string MonitoredManaged => _managedText;

        [Monitor("GC Heap", Group = "GC")]
        private string MonitoredHeap => _heapText;

        [Monitor("GC Alloc", Group = "Alloc")]
        private string MonitoredAlloc => _allocText;

        [MonitorGraph(Group = "Alloc", Min = 0, Max = 256)]
        private MonitorGraphData MonitoredAllocGraph = new(240);

        private long _lastManagedBytes;
        private float _allocKbThisFrame;

        private int _lastGen0;
        private int _lastGen1;
        private int _lastGen2;
        private int _deltaGen0;
        private int _deltaGen1;
        private int _deltaGen2;

        private ProfilerRecorder _gcAllocRecorder;
        private ProfilerRecorder _gcUsedRecorder;
        private ProfilerRecorder _gcReservedRecorder;

        private bool _recordersReady;
        private float _nextTextRefresh;
        private string _managedText = "";
        private string _heapText = "";
        private string _allocText = "";
        private string _collectionsText = "";

        private void OnEnable() => ResetState();

        private void OnDisable() => DisposeRecorders();

        private void OnDestroy() => DisposeRecorders();

        private void Update()
        {
            if (!_enabled)
                return;

            EnsureRecorders();

            var managedBytes = GC.GetTotalMemory(false);
            var managedMb = managedBytes / (1024f * 1024f);

            var allocFromDeltaKb = 0f;
            if (_lastManagedBytes > 0 && managedBytes >= _lastManagedBytes)
                allocFromDeltaKb = (managedBytes - _lastManagedBytes) / 1024f;

            _allocKbThisFrame = _gcAllocRecorder.Valid
                ? _gcAllocRecorder.LastValue / 1024f
                : allocFromDeltaKb;

            _lastManagedBytes = managedBytes;

            var gen0 = GC.CollectionCount(0);
            var gen1 = GC.CollectionCount(1);
            var gen2 = GC.CollectionCount(2);

            _deltaGen0 = gen0 - _lastGen0;
            _deltaGen1 = gen1 - _lastGen1;
            _deltaGen2 = gen2 - _lastGen2;

            _lastGen0 = gen0;
            _lastGen1 = gen1;
            _lastGen2 = gen2;

            MonitoredAllocGraph.Push(_allocKbThisFrame);

            if (Time.unscaledTime >= _nextTextRefresh)
            {
                _nextTextRefresh = Time.unscaledTime + 0.25f;

                _managedText = $"{managedMb:0.0} MB";

                _heapText = _gcUsedRecorder.Valid
                    ? $"Used {_gcUsedRecorder.LastValue / (1024f * 1024f):0.0} MB"
                    : "Used N/A";

                _heapText += _gcReservedRecorder.Valid
                    ? $" | Reserved {_gcReservedRecorder.LastValue / (1024f * 1024f):0.0} MB"
                    : " | Reserved N/A";

                _allocText = $"{_allocKbThisFrame:0} KB/frame";
                _collectionsText = $"Gen0 {_deltaGen0}, Gen1 {_deltaGen1}, Gen2 {_deltaGen2}";
            }
        }

        private void ResetState()
        {
            MonitoredAllocGraph.Clear();

            _lastManagedBytes = 0;
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
            _collectionsText = "";

            DisposeRecorders();
        }

        private void DisposeRecorders()
        {
            if (_gcAllocRecorder.Valid) _gcAllocRecorder.Dispose();
            if (_gcUsedRecorder.Valid) _gcUsedRecorder.Dispose();
            if (_gcReservedRecorder.Valid) _gcReservedRecorder.Dispose();
        }

        private void EnsureRecorders()
        {
            if (_recordersReady)
                return;

            try { _gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 15); } catch { }
            try { _gcUsedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory", 15); } catch { }
            try { _gcReservedRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Reserved Memory", 15); } catch { }

            _recordersReady = true;
        }
    }
}
