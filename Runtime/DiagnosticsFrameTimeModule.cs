using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;

namespace UnityEssentials
{
    [MonitorDock(MonitorCorner.TopLeft)]
    internal sealed class DiagnosticsFrameTimeModule : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private bool _trackRender = true;

        private float _fps;
        private float _avgFrameMs;
        private float _avgRenderMs;

        private readonly Stopwatch _renderTimer = new();
        private float _lastRenderMs;

        private float _accumTime;
        private int _accumFrames;
        private float _nextFpsSampleTime;

        private bool _hooked;

        [Monitor("FPS", Order = 0, Format = "0.0")]
        private float MonitoredFps => _fps;

        [Monitor("Frame", Order = 1)]
        private string MonitoredFrameMs
        {
            get
            {
                var ms = Mathf.Max(0.000001f, Time.unscaledDeltaTime) * 1000f;
                return $"{ms:0.00} ms (avg {_avgFrameMs:0.00} ms)";
            }
        }

        [Monitor("Render", Order = 2)]
        private string MonitoredRenderMs => $"{_lastRenderMs:0.00} ms (avg {_avgRenderMs:0.00} ms)";

        [Monitor("Target FrameRate", Order = 3)]
        private string MonitoredTargetFps =>
            Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString() : "Unlimited";

        [Monitor("VSync Count", Order = 4)]
        private int MonitoredVSyncCount => QualitySettings.vSyncCount;

        [MonitorGraph(Order = 10, Min = 0, Max = 50, Height = 80)]
        private MonitorGraphData MonitoredFrameMsGraph = new(240);

        [MonitorGraph(Order = 11, Min = 0, Max = 50, Height = 80)]
        private MonitorGraphData MonitoredRenderMsGraph = new(240);
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

            if (_trackRender)
                _renderTimer.Restart();

            var dt = Mathf.Max(0.000001f, Time.unscaledDeltaTime);
            var frameMs = dt * 1000f;

            MonitoredFrameMsGraph.Push(frameMs);
            MonitoredRenderMsGraph.Push(_lastRenderMs);

            _accumTime += dt;
            _accumFrames++;
            if (Time.unscaledTime >= _nextFpsSampleTime)
            {
                var period = Mathf.Max(0.15f, _accumTime);
                _fps = _accumFrames / period;
                _avgFrameMs = MonitoredFrameMsGraph.Average();
                _avgRenderMs = MonitoredRenderMsGraph.Average();

                _accumTime = 0;
                _accumFrames = 0;
                _nextFpsSampleTime = Time.unscaledTime + 0.25f;
            }
        }

        private void ResetState()
        {
            MonitoredFrameMsGraph.Clear();
            MonitoredRenderMsGraph.Clear();
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
    }
}
