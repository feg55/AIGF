using UnityEngine;

namespace Aigf.Companion.Room
{
    public sealed class RoomNode : MonoBehaviour
    {
        [SerializeField] private string id = "room_node";
        [SerializeField] private RoomNodeType type = RoomNodeType.Unknown;
        [SerializeField] private bool canSit;
        [SerializeField] private Collider boundsSource;
        [SerializeField] private Transform approachPoint;
        [SerializeField] private Transform interactionPoint;
        [SerializeField] private InteractionAnchor interactionAnchor;
        [SerializeField, TextArea] private string semanticMetadata;

        public string Id => id;
        public RoomNodeType Type => type;
        public bool CanSit => canSit && interactionAnchor != null && interactionAnchor.HasSittingGeometry;
        public Transform Anchor => transform;
        public Transform ApproachPoint => interactionAnchor != null && interactionAnchor.ApproachPoint != null
            ? interactionAnchor.ApproachPoint
            : approachPoint;
        public Transform InteractionPoint => interactionPoint != null ? interactionPoint : transform;
        public InteractionAnchor InteractionAnchor => interactionAnchor;
        public string SemanticMetadata => semanticMetadata ?? string.Empty;
        public Bounds Bounds => boundsSource != null
            ? boundsSource.bounds
            : new Bounds(transform.position, Vector3.one * 0.1f);

        public void Configure(
            string nodeId,
            RoomNodeType nodeType,
            bool nodeCanSit,
            Transform nodeApproachPoint = null,
            Transform nodeInteractionPoint = null,
            InteractionAnchor nodeInteractionAnchor = null,
            Collider nodeBoundsSource = null,
            string metadata = "")
        {
            id = (nodeId ?? string.Empty).Trim();
            type = nodeType;
            canSit = nodeCanSit;
            approachPoint = nodeApproachPoint;
            interactionPoint = nodeInteractionPoint;
            interactionAnchor = nodeInteractionAnchor;
            boundsSource = nodeBoundsSource;
            semanticMetadata = metadata ?? string.Empty;
        }
    }
}
