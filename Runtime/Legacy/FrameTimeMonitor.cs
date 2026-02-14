using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Unity.Essentials
{
    public class LegacyFrameTimeMonitor : MonoBehaviour
    {
        [SerializeField] private LegacyFrameTimeManager _manager;

        public IReadOnlyList<FrameData> FrameHistory => _frameHistory;
        private readonly List<FrameData> _frameHistory = new();
        private Stopwatch _renderTimer = new();

        private float _lastRenderTime;
        private long _lastMemoryBeforeGC;

        // GC tracking
        public List<GCCollectionInfo> GCCollections => _gcCollections;
        private readonly List<GCCollectionInfo> _gcCollections = new();
        private float _lastUsedMemory;
        private float _currentGCAlloc;

        public string HeapSizeText => _heapSizeText;
        private string _heapSizeText = "0";

        private float _graphWidth => Screen.width * _manager.GraphSize.x;

        public struct FrameData
        {
            public float TotalFrameTimeMs;
            public float RenderTimeMs;
            public bool GCRan;
            public float GCAllocation;
            public float MemoryUsageMB;
        }

        public struct GCCollectionInfo
        {
            public int CollectedMB;
            public int PositionX;
        }

        public void Start()
        {
            // Initialize with some default data
            for (int i = 0; i < 5000; i++)
            {
                _frameHistory.Add(new FrameData
                {
                    TotalFrameTimeMs = 1,
                    RenderTimeMs = 1,
                    GCAllocation = 0
                });
            }
        }

        public void Update()
        {
            TrackMemoryUsage();
            RecordFrameTime();
            UpdateGcMarkers();
        }

        public void LateUpdate() =>
            _renderTimer.Restart();

        public void TrackMemoryUsage()
        {
            long usedMemory = GC.GetTotalMemory(false);
            _heapSizeText = (GetTotalHeapSize() / (1024 * 1024)).ToString("0");

            float currentUsed = usedMemory / (1024 * 1024f);
            _currentGCAlloc = Mathf.Max(0, currentUsed - _lastUsedMemory);
            _lastUsedMemory = currentUsed;

            // Detect GC collections
            if (usedMemory < _lastMemoryBeforeGC)
            {
                int collectedMB = (int)((_lastMemoryBeforeGC - usedMemory) / (1024 * 1024));
                _gcCollections.Add(new GCCollectionInfo
                {
                    CollectedMB = collectedMB,
                    PositionX = (int)Mathf.Round(_graphWidth)
                });

                if (_frameHistory.Count > 0)
                {
                    var lastFrame = _frameHistory[^1];
                    lastFrame.GCRan = true;
                    _frameHistory[^1] = lastFrame;
                }
            }
            _lastMemoryBeforeGC = usedMemory;
        }

        private void RecordFrameTime()
        {
            float frameTimeMs = Time.deltaTime * 1000f;

            _frameHistory.Add(new FrameData
            {
                TotalFrameTimeMs = frameTimeMs,
                RenderTimeMs = _lastRenderTime,
                GCAllocation = _currentGCAlloc,
                GCRan = false,
                MemoryUsageMB = _lastUsedMemory
            });

            // Maintain history size
            while (_frameHistory.Count > _graphWidth)
                _frameHistory.RemoveAt(0);
        }

        private void UpdateGcMarkers()
        {
            for (int i = _gcCollections.Count - 1; i >= 0; i--)
            {
                var gcInfo = _gcCollections[i];
                gcInfo.PositionX--;
                _gcCollections[i] = gcInfo;

                if (gcInfo.PositionX <= 0)
                    _gcCollections.RemoveAt(i);
            }
        }

        public void OnEndCameraRendering()
        {
            _renderTimer.Stop();

            _lastRenderTime = (float)_renderTimer.Elapsed.TotalMilliseconds;
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR
        [DllImport("__Internal")]
        private static extern long mono_gc_get_heap_size();
#else
    private static long mono_gc_get_heap_size() => GC.GetTotalMemory(false);
#endif

        private long GetTotalHeapSize() => mono_gc_get_heap_size();
    }
}