using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace RaizesOcultas
{
    [RequireComponent(typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class FieldObject : MonoBehaviour
    {
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Transform leftController;
        [SerializeField] private Transform rightController;
        [SerializeField] private Renderer[] visualParts;
        [SerializeField] private float returnAfterSeconds;

        private XRGrabInteractable grab;
        private Rigidbody body;
        private Vector3 homePosition;
        private Quaternion homeRotation;
        private Color[] colors;
        private MaterialPropertyBlock flash;
        private bool highlighted;
        private float releasedAt = -1f;

        private void Awake()
        {
            grab = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            homePosition = transform.position;
            homeRotation = transform.rotation;
            flash = new MaterialPropertyBlock();
            colors = new Color[visualParts.Length];

            for (int index = 0; index < visualParts.Length; index++)
                colors[index] = visualParts[index].sharedMaterial.color;
        }

        private void OnEnable()
        {
            grab.selectEntered.AddListener(OnGrabbed);
            grab.selectExited.AddListener(OnReleased);
        }

        private void OnDisable()
        {
            grab.selectEntered.RemoveListener(OnGrabbed);
            grab.selectExited.RemoveListener(OnReleased);
        }

        public void Unlock()
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.WakeUp();
            grab.enabled = true;
        }

        private void LateUpdate()
        {
            if (!grab.enabled)
                return;

            bool near = !grab.isSelected && (IsNear(leftController) || IsNear(rightController));
            Pulse(near);

            bool leftSite = transform.position.y < -0.50f ||
                            Mathf.Abs(transform.position.x) > 7.2f ||
                            transform.position.z < -4.8f || transform.position.z > 9.8f;
            bool expired = returnAfterSeconds > 0f && releasedAt >= 0f &&
                           Time.time >= releasedAt + returnAfterSeconds;
            if (!grab.isSelected && (leftSite || expired))
                ReturnToPlace();
        }

        private bool IsNear(Transform hand)
        {
            return hand.gameObject.activeInHierarchy &&
                   (hand.position - transform.position).sqrMagnitude < 0.33f * 0.33f;
        }

        private void Pulse(bool near)
        {
            if (!near && !highlighted)
                return;

            float intensity = 0.23f + 0.72f * Mathf.PingPong(Time.unscaledTime * 4f, 1f);
            for (int index = 0; index < visualParts.Length; index++)
            {
                Renderer part = visualParts[index];
                part.GetPropertyBlock(flash);
                flash.SetColor(BaseColor, near ? Color.Lerp(colors[index], Color.white, intensity) : colors[index]);
                part.SetPropertyBlock(flash);
            }
            highlighted = near;
        }

        private void OnGrabbed(SelectEnterEventArgs args)
        {
            releasedAt = -1f;
            if (args.interactorObject is XRBaseInputInteractor controller)
                controller.SendHapticImpulse(0.24f, 0.045f);
        }

        private void OnReleased(SelectExitEventArgs args)
        {
            releasedAt = Time.time;
        }

        private void ReturnToPlace()
        {
            body.position = homePosition;
            body.rotation = homeRotation;
            if (!body.isKinematic)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            releasedAt = -1f;
        }
    }
}
