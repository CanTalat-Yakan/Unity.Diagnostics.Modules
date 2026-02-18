using System;
using System.Collections.Generic;
using ImGuiNET;
using UnityEngine;

namespace UnityEssentials
{
    internal sealed class ImGuiConsoleModule : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;
        [SerializeField] private string _windowTitle = "Console";

        private readonly object _lock = new();
        private readonly List<LogEntry> _entries = new(256);

        [SerializeField] private int _maxEntries = 200;
        [SerializeField] private bool _paused;
        [SerializeField] private bool _showStacktraces;
        [SerializeField] private bool _autoScroll = true;

        [SerializeField] private string _filter = "";

        private bool _hooked;

        private struct LogEntry
        {
            public DateTime TimeUtc;
            public LogType Type;
            public string Message;
            public string StackTrace;
            public int Count;
        }
        
        private void OnEnable() => EnsureHooked();
        
        private void OnDisable() => Unhook();
        
        private void OnDestroy() => Unhook();
        
        private void Update()
        {
            if (!_enabled)
                return;
        
            DrawImGui();
        }

        private void EnsureHooked()
        {
            if (_hooked)
                return;

            Application.logMessageReceivedThreaded -= OnLog;
            Application.logMessageReceivedThreaded += OnLog;
            _hooked = true;
        }

        private void Unhook()
        {
            if (!_hooked)
                return;

            Application.logMessageReceivedThreaded -= OnLog;
            _hooked = false;
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            if (_paused)
                return;

            lock (_lock)
            {
                // Coalesce duplicates at tail.
                if (_entries.Count > 0)
                {
                    var last = _entries[^1];
                    if (last.Type == type && last.Message == condition && last.StackTrace == stackTrace)
                    {
                        last.Count++;
                        _entries[^1] = last;
                        return;
                    }
                }

                _entries.Add(new LogEntry
                {
                    TimeUtc = DateTime.UtcNow,
                    Type = type,
                    Message = condition ?? string.Empty,
                    StackTrace = stackTrace ?? string.Empty,
                    Count = 1
                });

                var max = Mathf.Clamp(_maxEntries, 50, 20000);
                if (_entries.Count > max)
                    _entries.RemoveRange(0, _entries.Count - max);
            }
        }

        private void DrawImGui()
        {
            using var scope = ImGuiScope.TryEnter();
            if (!scope.Active)
                return;

            if (!ImGui.Begin(_windowTitle))
            {
                ImGui.End();
                return;
            }

            ImGui.TextUnformatted("In-game console");
            ImGui.Separator();

            if (ImGui.Button("Clear"))
            {
                lock (_lock)
                    _entries.Clear();
            }
            ImGui.SameLine();
            ImGui.Checkbox("Paused", ref _paused);
            ImGui.SameLine();
            ImGui.Checkbox("Auto-scroll", ref _autoScroll);
            ImGui.SameLine();
            ImGui.Checkbox("Stacktraces", ref _showStacktraces);

            ImGui.SliderInt("Max", ref _maxEntries, 50, 2000);

            ImGui.InputTextWithHint("Filter", "type to filter (substring)", ref _filter, 256);

            ImGui.Spacing();

            var available = ImGui.GetContentRegionAvail();
            if (!ImGui.BeginChild("ConsoleScroll", new System.Numerics.Vector2(available.X, available.Y), ImGuiChildFlags.Borders))
            {
                ImGui.EndChild();
                ImGui.End();
                return;
            }

            List<LogEntry> snapshot;
            lock (_lock)
                snapshot = new List<LogEntry>(_entries);

            for (var i = 0; i < snapshot.Count; i++)
            {
                var e = snapshot[i];

                if (!string.IsNullOrEmpty(_filter))
                {
                    if (e.Message == null || e.Message.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                }

                var col = GetColor(e.Type);
                ImGui.PushStyleColor(ImGuiCol.Text, col);

                var prefix = e.TimeUtc.ToString("HH:mm:ss");
                var countSuffix = e.Count > 1 ? $" (x{e.Count})" : string.Empty;
                ImGui.TextWrapped($"[{prefix}] [{e.Type}] {e.Message}{countSuffix}");

                ImGui.PopStyleColor();

                if (_showStacktraces && !string.IsNullOrWhiteSpace(e.StackTrace))
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.65f, 0.7f, 0.75f, 1f));
                    ImGui.TextWrapped(e.StackTrace);
                    ImGui.PopStyleColor();
                }

                ImGui.Separator();
            }

            if (_autoScroll && ImGui.GetScrollY() >= ImGui.GetScrollMaxY() - 1f)
                ImGui.SetScrollHereY(1f);

            ImGui.EndChild();
            ImGui.End();
        }

        private static System.Numerics.Vector4 GetColor(LogType type)
        {
            return type switch
            {
                LogType.Error or LogType.Assert or LogType.Exception => new System.Numerics.Vector4(1f, 0.5f, 0.52f, 1f),
                LogType.Warning => new System.Numerics.Vector4(1f, 0.96f, 0.56f, 1f),
                _ => new System.Numerics.Vector4(0.8f, 0.75f, 1f, 1f)
            };
        }
    }
}
