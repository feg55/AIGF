using UnityEngine;

namespace Aigf.Companion.Room
{
    public static class SeatGeometry
    {
        public static void Calculate(BoxCollider box, Vector3 observer, bool sofa,
            out Vector3 approach, out Vector3 seat, out Quaternion facing)
        {
            var t = box.transform;
            var center = t.TransformPoint(box.center);
            var axes = new[] { t.TransformVector(Vector3.right), t.TransformVector(Vector3.up), t.TransformVector(Vector3.forward) };
            var size = box.size;
            var depthAxis = -1;
            var smallest = float.PositiveInfinity;
            // PICO legacy anchors may use Z as vertical. Never treat local Y as
            // world height, or carry anchor pitch/roll into the avatar root.
            for (var i = 2; i >= 0; i--)
            {
                if (Mathf.Abs(Vector3.Dot(axes[i].normalized, Vector3.up)) > 0.7f) continue;
                var length = Mathf.Abs(size[i]) * axes[i].magnitude;
                if (length >= smallest - 0.001f) continue;
                smallest = length;
                depthAxis = i;
            }
            var forward = depthAxis >= 0 ? Vector3.ProjectOnPlane(axes[depthAxis], Vector3.up).normalized : Vector3.forward;
            // Box bounds have no front/back semantic. Choose the depth-axis side
            // facing the occupied room, consistently for both approach and sitting.
            if (Vector3.Dot(forward, observer - center) < 0f) forward = -forward;
            var halfHeight = 0f;
            var halfDepth = 0f;
            for (var i = 0; i < 3; i++)
            {
                halfHeight += Mathf.Abs(Vector3.Dot(axes[i], Vector3.up) * size[i]) * 0.5f;
                halfDepth += Mathf.Abs(Vector3.Dot(axes[i], forward) * size[i]) * 0.5f;
            }
            var bottom = center.y - halfHeight;
            var seatHeight = sofa ? Mathf.Clamp(halfHeight * 0.9f, 0.38f, 0.52f) :
                Mathf.Clamp(halfHeight * 0.96f, 0.4f, 0.58f);
            approach = center + forward * (halfDepth + 0.45f);
            approach.y = bottom;
            seat = center + forward * Mathf.Min(0.18f, halfDepth * 0.3f);
            seat.y = bottom + seatHeight;
            facing = Quaternion.LookRotation(forward, Vector3.up);
        }
    }
}
