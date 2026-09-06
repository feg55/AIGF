using UnityEngine;
using UnityEngine.UI;

namespace Aigf.Companion.UI
{
    // Vector UI geometry keeps the symbol crisp in both eyes without a texture dependency.
    public sealed class MicrophoneIcon : MaskableGraphic
    {
        private bool crossed;

        public void SetState(Color tint, bool isCrossed)
        {
            if (color != tint) color = tint;
            if (crossed == isCrossed) return;
            crossed = isCrossed;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Line(mesh, new Vector2(-7, 0), new Vector2(-7, 16), 3);
            Line(mesh, new Vector2(7, 0), new Vector2(7, 16), 3);
            Arc(mesh, new Vector2(0, 16), 7, 0, 180);
            Arc(mesh, Vector2.zero, 7, 180, 360);
            Arc(mesh, Vector2.zero, 14, 180, 360);
            Line(mesh, new Vector2(-14, 0), new Vector2(-14, 5), 3);
            Line(mesh, new Vector2(14, 0), new Vector2(14, 5), 3);
            Line(mesh, new Vector2(0, -14), new Vector2(0, -23), 3);
            Line(mesh, new Vector2(-8, -23), new Vector2(8, -23), 3);
            if (crossed) Line(mesh, new Vector2(-21, 23), new Vector2(21, -23), 4);
        }

        private void Arc(VertexHelper mesh, Vector2 center, float radius, float from, float to)
        {
            for (var i = 0; i < 16; i++)
            {
                var a = Mathf.Lerp(from, to, i / 16f) * Mathf.Deg2Rad;
                var b = Mathf.Lerp(from, to, (i + 1) / 16f) * Mathf.Deg2Rad;
                Line(mesh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                    center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius, 3);
            }
        }

        private void Line(VertexHelper mesh, Vector2 a, Vector2 b, float width)
        {
            var normal = new Vector2(-(b - a).y, (b - a).x).normalized * width * 0.5f;
            var index = mesh.currentVertCount;
            mesh.AddVert(a - normal, color, Vector2.zero);
            mesh.AddVert(a + normal, color, Vector2.zero);
            mesh.AddVert(b + normal, color, Vector2.zero);
            mesh.AddVert(b - normal, color, Vector2.zero);
            mesh.AddTriangle(index, index + 1, index + 2);
            mesh.AddTriangle(index, index + 2, index + 3);
        }
    }
}
