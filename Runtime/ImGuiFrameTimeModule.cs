using System;
using System.Diagnostics;
using ImGuiNET;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEssentials
{
    internal sealed class ImGuiFrameTimeModule : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private string _windowTitle = "Frame";

        private const int HistorySize = 240;

        private readonly float[] _frameMs = new float[HistorySize];
        private readonly float[] _renderMs = new float[HistorySize];
        private readonly float[] _scratch = new float[HistorySize];
        private int _cursor;
        private int _count;

        private float _fps;
        private float _avgFrameMs;
        private float _avgRenderMs;

        private readonly Stopwatch _renderTimer = new();
        private float _lastRenderMs;

        private float _accumTime;
        private int _accumFrames;
        private float _nextFpsSampleTime;

        private bool _hooked;
        [SerializeField] private bool _trackRender = true;
        
        private void OnEnable()
        {
            ResetState();
            EnsureHooks();
        }
        
        private void OnDisable() => Unhook();
        
        private void OnDestroy() => Unhook();

        private void Update()
        {
            if (!_enabled)
                return;
        
            DrawImGui();
        }
        
        private void ResetState()
        {
            Array.Clear(_frameMs, 0, _frameMs.Length);
            Array.Clear(_renderMs, 0, _renderMs.Length);
            Array.Clear(_scratch, 0, _scratch.Length);
            _cursor = 0;
            _count = 0;
            _fps = 0;
            _avgFrameMs = 0;
            _avgRenderMs = 0;
            _lastRenderMs = 0;
            _accumTime = 0;
            _accumFrames = 0;
            _nextFpsSampleTime = 0;
        }

        private void EnsureHooks()
        {
            if (_hooked)
                return;

            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            RenderPipelineManager.endCameraRendering += OnEndCameraRendering;
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked)
                return;

            RenderPipelineManager.endCameraRendering -= OnEndCameraRendering;
            _hooked = false;
        }

        private void OnEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_trackRender)
                return;

            if (camera == null || camera.cameraType != CameraType.Game)
                return;

            _renderTimer.Stop();
            _lastRenderMs = (float)_renderTimer.Elapsed.TotalMilliseconds;
        }

        private void DrawImGui()
        {
            if (!ImGui.Begin(_windowTitle))
            {
                ImGui.End();
                return;
            }

            // Measure render time using the same approach as FrameTimeMonitor: restart late-ish, stop at endCameraRendering.
            if (_trackRender)
                _renderTimer.Restart();

            var dt = Mathf.Max(0.000001f, Time.unscaledDeltaTime);
            var frameMs = dt * 1000f;

            // Update rolling history
            _frameMs[_cursor] = frameMs;
            _renderMs[_cursor] = _lastRenderMs;
            _cursor = (_cursor + 1) % HistorySize;
            _count = Mathf.Min(HistorySize, _count + 1);

            // Throttled FPS calculation to avoid noisy display.
            _accumTime += dt;
            _accumFrames++;
            if (Time.unscaledTime >= _nextFpsSampleTime)
            {
                var period = Mathf.Max(0.15f, _accumTime);
                _fps = _accumFrames / period;

                // Averages over current buffer.
                var n0 = _count;
                double sumF = 0;
                double sumR = 0;
                for (var i = 0; i < n0; i++)
                {
                    sumF += _frameMs[i];
                    sumR += _renderMs[i];
                }

                _avgFrameMs = n0 > 0 ? (float)(sumF / n0) : 0f;
                _avgRenderMs = n0 > 0 ? (float)(sumR / n0) : 0f;

                _accumTime = 0;
                _accumFrames = 0;
                _nextFpsSampleTime = Time.unscaledTime + 0.25f;
            }

            ImGui.TextUnformatted("Frame timing");
            ImGui.Separator();

            ImGui.TextUnformatted($"FPS: {_fps:0.0}");
            ImGui.TextUnformatted($"Frame: {frameMs:0.00} ms (avg {_avgFrameMs:0.00} ms)");
            ImGui.TextUnformatted($"Render: {_lastRenderMs:0.00} ms (avg {_avgRenderMs:0.00} ms)");
            ImGui.TextUnformatted($"Target FrameRate: {(Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "Unlimited")}");
            ImGui.TextUnformatted($"VSync Count: {QualitySettings.vSyncCount}");

            ImGui.Spacing();

            ImGui.Checkbox("Track render time (SRP endCameraRendering)", ref _trackRender);

            ImGui.Spacing();

            // Graphs (ImGui plots expect contiguous arrays in plotting order; our ring buffer is circular).
            var n = _count;
            for (var i = 0; i < n; i++)
            {
                var idx = (_cursor - n + i);
                if (idx < 0) idx += HistorySize;
                _scratch[i] = _frameMs[idx];
            }

            if (n > 0)
                ImGui.PlotLines("Frame ms", ref _scratch[0], n, 0, null, 0f, 50f, new System.Numerics.Vector2(0, 80));

            for (var i = 0; i < n; i++)
            {
                var idx = (_cursor - n + i);
                if (idx < 0) idx += HistorySize;
                _scratch[i] = _renderMs[idx];
            }

            if (n > 0)
                ImGui.PlotLines("Render ms", ref _scratch[0], n, 0, null, 0f, 50f, new System.Numerics.Vector2(0, 80));

            ImGui.End();
        }
    }
}
