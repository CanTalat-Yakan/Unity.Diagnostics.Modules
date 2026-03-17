using System;
using ImGuiNET;
using UnityEngine;
using Vector2 = System.Numerics.Vector2;
using Vector4 = System.Numerics.Vector4;

namespace UnityEssentials
{
    /// <summary>
    /// Full-width ImGui FPS graph pinned to the bottom of the screen.
    /// Logarithmically-scaled frame-time history with colored reference lines
    /// at the target, half, and quarter refresh rates.
    /// </summary>
    internal sealed class DiagnosticsFpsGraphModule : MonoBehaviour
    {
        private const int BufferSize = 960;
        private const float MaxFrameTimeMs = 1000f; // 1 FPS

        private float[] _buffer;
        private int _cursor;
        private int _count;

        private int _targetRefreshRate;

        private float[] _plotScratch;

        private void OnEnable()
        {
            _buffer ??= new float[BufferSize];
            _plotScratch ??= new float[BufferSize];
            _cursor = 0;
            _count = BufferSize;
        }

        private void Update()
        {
            if (_buffer == null || _plotScratch == null)
                return;

            _targetRefreshRate = Mathf.Max(30, (int)Screen.currentResolution.refreshRateRatio.value);

            float dt = Mathf.Max(0.000001f, Time.unscaledDeltaTime);
            float frameMs = dt * 1000f;

            // Push into ring buffer.
            _buffer[_cursor] = frameMs;
            _cursor = (_cursor + 1) % BufferSize;
            if (_count < BufferSize) _count++;

            DrawImGui();
        }

        private void DrawImGui()
        {
            using var scope = ImGuiScope.TryEnter();
            if (!scope.Active)
                return;

            var io = ImGui.GetIO();
            float screenW = io.DisplaySize.X;
            float screenH = io.DisplaySize.Y;
            if (screenW <= 0 || screenH <= 0)
                return;

            float windowH = 80f + 24f;

            ImGui.SetNextWindowPos(new Vector2(0, screenH - windowH), ImGuiCond.Always);
            ImGui.SetNextWindowSize(new Vector2(screenW, windowH), ImGuiCond.Always);
            ImGui.SetNextWindowBgAlpha(0.25f);

            const ImGuiWindowFlags flags =
                ImGuiWindowFlags.NoDecoration |
                ImGuiWindowFlags.NoSavedSettings |
                ImGuiWindowFlags.NoFocusOnAppearing |
                ImGuiWindowFlags.NoNav |
                ImGuiWindowFlags.NoMove |
                ImGuiWindowFlags.NoResize |
                ImGuiWindowFlags.NoInputs;

            if (!ImGui.Begin("##FpsGraph", flags))
            {
                ImGui.End();
                return;
            }

            int n = CopyLinearized(_plotScratch);
            if (n > 0)
            {
                // Logarithmic normalization so the region near the target is expanded.
                for (int i = 0; i < n; i++)
                    _plotScratch[i] = NormalizeFrameTime(_plotScratch[i]);

                var plotPos = ImGui.GetCursorScreenPos();
                float plotW = ImGui.GetContentRegionAvail().X;
                float plotH = 80f;

                ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0, 0, 0, 0.15f));
                ImGui.PushStyleColor(ImGuiCol.PlotLines, new Vector4(0.2f, 0.9f, 0.3f, 1f));
                ImGui.PlotLines(" ", ref _plotScratch[0], n, 0, null,
                    0f, 1f, new Vector2(plotW, plotH));
                ImGui.PopStyleColor(2);

                // Reference lines drawn over the plot.
                var dl = ImGui.GetWindowDrawList();

                DrawRefLine(dl, plotPos, plotW, plotH,
                    1000f / _targetRefreshRate,
                    ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 1f, 0f, 0.6f)),
                    $"{_targetRefreshRate} fps");

                DrawRefLine(dl, plotPos, plotW, plotH,
                    1000f / (_targetRefreshRate / 2f),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 1f, 0f, 0.6f)),
                    $"{_targetRefreshRate / 2} fps");

                DrawRefLine(dl, plotPos, plotW, plotH,
                    1000f / (_targetRefreshRate / 4f),
                    ImGui.ColorConvertFloat4ToU32(new Vector4(1f, 0.5f, 0f, 0.6f)),
                    $"{_targetRefreshRate / 4} fps");
            }

            ImGui.End();
        }

        private void DrawRefLine(ImDrawListPtr dl, Vector2 plotOrigin,
            float plotWidth, float plotHeight, float frameTimeMs, uint color, string label)
        {
            float norm = NormalizeFrameTime(frameTimeMs);
            float y = plotOrigin.Y + plotHeight * (1f - norm);

            dl.AddLine(
                new Vector2(plotOrigin.X, y),
                new Vector2(plotOrigin.X + plotWidth, y),
                color, 1.5f);

            dl.AddText(new Vector2(plotOrigin.X + 4f, y - 14f), color, label);
        }

        /// <summary>
        /// Reciprocal-power normalization (power = −1) that expands the region near the target
        /// frame-time and compresses the high-latency tail.
        /// Returns 0 at the target frame-time and 1 at <see cref="MaxFrameTimeMs"/>.
        /// </summary>
        private float NormalizeFrameTime(float frameTimeMs)
        {
            float targetMs = 1000f / _targetRefreshRate;
            float clamped = Mathf.Clamp(frameTimeMs, targetMs, MaxFrameTimeMs);

            float min = Pow(targetMs, -1f);
            float max = Pow(MaxFrameTimeMs, -1f);
            float denom = max - min;

            // Guard against degenerate cases that would produce NaN.
            if (Mathf.Abs(denom) < 1e-12f)
                return 0f;

            float val = Pow(clamped, -1f);
            return Mathf.Clamp01((val - min) / denom);
        }

        private static float Pow(float a, float power) => Mathf.Pow(a, power);

        private int CopyLinearized(float[] dest)
        {
            int count = _count;
            for (int i = 0; i < count; i++)
            {
                int idx = _cursor - count + i;
                if (idx < 0) idx += BufferSize;
                dest[i] = _buffer[idx];
            }
            return count;
        }
    }
}