using UnityEngine;

namespace Aigf.Companion.Room
{
    public sealed class InteractionAnchor : MonoBehaviour
    {
        [SerializeField] private Transform approachPoint;
        [SerializeField] private Transform sitPoint;
        [SerializeField] private Transform sitFacingReference;

        public Transform ApproachPoint => approachPoint;
        public Transform SitPoint => sitPoint;
        public Quaternion SitRotation
        {
            get
            {
                var reference = sitFacingReference != null ? sitFacingReference : sitPoint != null ? sitPoint : transform;
                var forward = Vector3.ProjectOnPlane(reference.forward, Vector3.up);
                if (forward.sqrMagnitude < 0.001f && approachPoint != null && sitPoint != null)
                    forward = Vector3.ProjectOnPlane(approachPoint.position - sitPoint.position, Vector3.up);
                return Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward : Vector3.forward, Vector3.up);
            }
        }
        public bool HasSittingGeometry => approachPoint != null && sitPoint != null;

        public void Configure(Transform approach, Transform sit, Transform facingReference = null)
        {
            approachPoint = approach;
            sitPoint = sit;
            sitFacingReference = facingReference;
        }
    }
}
