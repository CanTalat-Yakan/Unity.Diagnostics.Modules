using UnityEngine;

namespace Unity.Essentials
{
    [RequireComponent(typeof(LegacyFrameTimeMonitor))]
    public class LegacyFrameTimeGUI : MonoBehaviour
    {
        [SerializeField] private LegacyFrameTimeManager _manager;
        [SerializeField] private LegacyFrameTimeMonitor _monitor;

        [Space]
        [SerializeField] private Font _customFont;

        private float _bottomLeftPivotX => Screen.width * _manager.BottomLeftPivot.x;
        private float _bottomLeftPivotY => Screen.height * _manager.BottomLeftPivot.y;
        private float _graphWidth => Screen.width * _manager.GraphSize.x;
        private int _scale => Mathf.Max(1, Screen.width / 1920);

        public void OnGUI()
        {
            if (!_manager.DisplayStatistics || _monitor == null || _monitor.FrameHistory.Count == 0)
                return;

            var lastFrame = _monitor.FrameHistory[^1];

            float baseX = _bottomLeftPivotX + _graphWidth + 5;
            float baseY = Screen.height - _bottomLeftPivotY;
            float graphHeight = _graphWidth;

            // Current FPS
            LabelWithShadow(new Rect(baseX, baseY - 10 * _scale, 100, 20), $"FPS:    {1f / (lastFrame.TotalFrameTimeMs / 1000f):000.0}");
            LabelWithShadow(new Rect(baseX, baseY - 30 * _scale, 100, 20), $"Frame:  {lastFrame.TotalFrameTimeMs:00.00}ms");
            LabelWithShadow(new Rect(baseX, baseY - 45 * _scale, 100, 20), $"Render: {lastFrame.RenderTimeMs:00.00}ms");

            // Memory info
            LabelWithShadow(new Rect(baseX, baseY + 10 * _scale, 300, 20), $"Memory: {lastFrame.MemoryUsageMB:0.0}MB / {_monitor.HeapSizeText:0.0}MB");
            LabelWithShadow(new Rect(baseX - 15 * _scale, baseY + 25 * _scale, 200, 20), "← GC Allocation");

            // GC collection amounts
            foreach (var gcInfo in _monitor.GCCollections)
            {
                float xPos = _bottomLeftPivotX + gcInfo.PositionX;
                LabelWithShadow(new Rect(xPos, baseY + 25 * _scale, 30, 20), gcInfo.CollectedMB.ToString());
                LabelWithShadow(new Rect(xPos + 20 * _scale, baseY + 25 * _scale, 30, 20), "MB");
            }
        }

        private void LabelWithShadow(Rect rect, string text)
        {
            var style = new GUIStyle();
            style.font = _customFont;
            style.normal.textColor = Color.white;
            style.fontSize = 12 * _scale;

            Color oldColor = GUI.color;
            GUI.color = Color.black;
            GUI.Label(new Rect(rect.x + 1, rect.y + 1, rect.width, rect.height), text, style);
            GUI.color = oldColor;
            GUI.Label(rect, text, style);
        }
    }
}