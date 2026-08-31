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
        public Quaternion SitRotation => sitFacingReference != null
            ? sitFacingReference.rotation
            : sitPoint != null ? sitPoint.rotation : transform.rotation;
        public bool HasSittingGeometry => approachPoint != null && sitPoint != null;

        public void Configure(Transform approach, Transform sit, Transform facingReference = null)
        {
            approachPoint = approach;
            sitPoint = sit;
            sitFacingReference = facingReference;
        }
    }
}
