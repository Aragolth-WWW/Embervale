using UnityEngine;

namespace Embervale.Animation
{
    // Lightweight humanoid foot IK using Animator IK callbacks.
    // Requires the Animator to be Humanoid and IK Pass enabled on the base layer.
    [RequireComponent(typeof(Animator))]
    public class SimpleFootIK : MonoBehaviour
    {
        [SerializeField] private LayerMask groundLayers = Physics.DefaultRaycastLayers;
        [SerializeField] private float raycastOrigin = 0.5f;
        [SerializeField] private float raycastDistance = 1.5f;
        [SerializeField] private float footOffset = 0.02f;
        [SerializeField] private float alignSpeed = 10f;

        private Animator _anim;

        private void Awake()
        {
            _anim = GetComponent<Animator>();
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (_anim == null) return;

            SolveFoot(AvatarIKGoal.LeftFoot);
            SolveFoot(AvatarIKGoal.RightFoot);
        }

        private void SolveFoot(AvatarIKGoal goal)
        {
            var footPos = _anim.GetIKPosition(goal);
            var origin = footPos + Vector3.up * raycastOrigin;
            if (Physics.Raycast(origin, Vector3.down, out var hit, raycastDistance, groundLayers, QueryTriggerInteraction.Ignore))
            {
                var targetPos = hit.point + Vector3.up * footOffset;
                var targetRot = Quaternion.FromToRotation(transform.up, hit.normal) * transform.rotation;

                _anim.SetIKPositionWeight(goal, 1f);
                _anim.SetIKRotationWeight(goal, 1f);

                var curPos = _anim.GetIKPosition(goal);
                var curRot = _anim.GetIKRotation(goal);
                var t = Mathf.Clamp01(alignSpeed * Time.deltaTime);
                _anim.SetIKPosition(goal, Vector3.Lerp(curPos, targetPos, t));
                _anim.SetIKRotation(goal, Quaternion.Slerp(curRot, targetRot, t));
            }
            else
            {
                _anim.SetIKPositionWeight(goal, 0f);
                _anim.SetIKRotationWeight(goal, 0f);
            }
        }
    }
}

