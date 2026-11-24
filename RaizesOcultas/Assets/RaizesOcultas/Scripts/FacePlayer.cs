using UnityEngine;

namespace RaizesOcultas
{
    public sealed class FacePlayer : MonoBehaviour
    {
        [SerializeField] private Transform viewer;
        [SerializeField] private bool upright = true;

        private void LateUpdate()
        {
            Vector3 direction = transform.position - viewer.position;
            if (upright)
                direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}
