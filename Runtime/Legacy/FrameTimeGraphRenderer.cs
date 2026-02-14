using UnityEngine;

namespace Unity.Essentials
{
    [RequireComponent(typeof(LegacyFrameTimeMonitor))]
    public class LegacyFrameTimeGraphRenderer : MonoBehaviour
    {
        [SerializeField] private LegacyFrameTimeManager _manager;
        [SerializeField] private LegacyFrameTimeMonitor _monitor;

        private Material _graphMaterial;
        private const float MaxFrameTime = 1000f; // 1 FPS

        private int _targetRefreshRate => _manager.TargetRefreshRate;
        private float _bottomLeftPivotX => Screen.width * _manager.BottomLeftPivot.x;
        private float _bottomLeftPivotY => Screen.height * _manager.BottomLeftPivot.y;
        private float _graphHeight => Screen.height * _manager.GraphSize.y;
        private float _graphWidth => Screen.width * _manager.GraphSize.x;

        public void Awake()
        {
            var shader = Shader.Find("Hidden/Internal-Colored");
            _graphMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        }

        public void DrawFrameTimeGraph()
        {
            if (!_manager.DisplayStatistics || _monitor == null)
                return;

            GL.PushMatrix();
            _graphMaterial.SetPass(0);
            GL.LoadPixelMatrix(0, Screen.width, 0, Screen.height);

            float baseX = Mathf.Round(_bottomLeftPivotX);
            float baseY = Mathf.Round(_bottomLeftPivotY);

            // Use GL.QUADS instead of GL.LINES for thicker, more reliable rendering
            GL.Begin(GL.QUADS);

            // Draw reference lines (now 2 pixels tall for better visibility)
            DrawReferenceQuad(baseX, baseY, 1000f / _targetRefreshRate, Color.green); // Target FPS
            DrawReferenceQuad(baseX, baseY, 1000f / (_targetRefreshRate / 2f), Color.yellow); // Half FPS
            DrawReferenceQuad(baseX, baseY, 1000f / (_targetRefreshRate / 4f), new Color(1, 0.5f, 0)); // Quarter FPS
            DrawReferenceQuad(baseX, baseY, MaxFrameTime, Color.red); // 1 FPS

            // Draw frame data
            var history = _monitor.FrameHistory;
            for (int i = 0; i < history.Count; i++)
            {
                LegacyFrameTimeMonitor.FrameData frame = history[i];
                // Align to pixel grid and make each bar 1 pixel wide
                float xPos = Mathf.Round(baseX + i);

                // Normalize times (0 = best, 1 = worst)
                float normalizedTime = NormalizeFrameTime(frame.TotalFrameTimeMs);
                float normalizedRender = NormalizeFrameTime(frame.RenderTimeMs);

                // Calculate heights (rounded to nearest pixel)
                float totalHeight = Mathf.Round(normalizedTime * _graphHeight);
                float renderHeight = Mathf.Round(normalizedRender * _graphHeight);

                // Frame time (green/magenta) - 1 pixel wide bar
                GL.Color(frame.GCRan ? Color.magenta : Color.green);
                DrawQuad(xPos, baseY, 1, totalHeight);

                // Render time (blue) - 1 pixel wide bar
                GL.Color(Color.blue);
                DrawQuad(xPos, baseY, 1, renderHeight);

                // GC allocation (purple) - 1 pixel wide bar
                float allocHeight = Mathf.Round(frame.GCAllocation * 5f); // Scaled down
                GL.Color(new Color(1, 0, 1, 0.5f));
                DrawQuad(xPos, baseY - allocHeight, 1, allocHeight);
            }

            GL.End();
            GL.PopMatrix();
        }

        private void DrawQuad(float x, float y, float width, float height)
        {
            // Draw a solid quad instead of a line for better visibility
            GL.Vertex3(x, y, 0);
            GL.Vertex3(x + width, y, 0);
            GL.Vertex3(x + width, y + height, 0);
            GL.Vertex3(x, y + height, 0);
        }

        private void DrawReferenceQuad(float baseX, float baseY, float ms, Color color)
        {
            float normalized = NormalizeFrameTime(ms);
            float yPos = Mathf.Round(baseY + (normalized * _graphHeight));

            // Draw a 2-pixel tall line for better visibility
            GL.Color(color);
            DrawQuad(baseX, yPos - 1, _graphWidth, 2);
        }

        private float NormalizeFrameTime(float frameTimeMs)
        {
            float power = -1f;

            // Logarithmic scaling for better visualization
            float min = Pow(1000f / _targetRefreshRate, power);
            float max = Pow(MaxFrameTime, power);
            float value = Pow(Mathf.Clamp(frameTimeMs, 1000f / _targetRefreshRate, MaxFrameTime), power);

            return (value - min) / (max - min);
        }

        private float Pow(float a, float power) =>
            Mathf.Pow(a, power);

        public void OnDestroy()
        {
            if (_graphMaterial != null)
                Destroy(_graphMaterial);
        }
    }
}