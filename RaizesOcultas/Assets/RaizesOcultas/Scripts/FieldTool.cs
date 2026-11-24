using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace RaizesOcultas
{
    [RequireComponent(typeof(XRGrabInteractable))]
    public sealed class FieldTool : MonoBehaviour
    {
        public enum Kind { Shovel, Brush }

        [SerializeField] private Kind kind;
        [SerializeField] private Transform tip;
        [SerializeField] private FieldSite[] sites;
        [SerializeField] private FieldFeedback feedback;

        private XRGrabInteractable grab;
        private Vector3 lastTip;
        private bool hasSample;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
        }

        private void LateUpdate()
        {
            if (!grab.isSelected)
            {
                if (hasSample)
                    ResetMotion();
                return;
            }

            Vector3 currentTip = tip.position;
            if (!hasSample)
            {
                lastTip = currentTip;
                hasSample = true;
                return;
            }

            Vector3 previousTip = lastTip;
            float stepSquared = (currentTip - previousTip).sqrMagnitude;
            // Acumula movimentos lentos até que a ponta saia da margem de tremor.
            if (stepSquared < 0.003f * 0.003f)
                return;

            lastTip = currentTip;
            // Descarta saltos de rastreamento ou teletransporte.
            if (stepSquared > 0.35f * 0.35f)
            {
                ResetMotion();
                return;
            }

            foreach (FieldSite site in sites)
            {
                if (!site.ApplyStroke(kind, previousTip, currentTip, out Vector3 contact))
                    continue;

                feedback.Stroke(kind, contact, grab);
                break;
            }
        }

        private void OnDisable()
        {
            ResetMotion();
        }

        private void ResetMotion()
        {
            hasSample = false;
            if (sites == null)
                return;
            foreach (FieldSite site in sites)
            {
                if (site != null)
                    site.ResetToolMotion(kind);
            }
        }
    }
}
