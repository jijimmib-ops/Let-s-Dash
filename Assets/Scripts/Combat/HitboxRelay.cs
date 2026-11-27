using UnityEngine;

namespace LetsDash.Combat
{
    public interface IDamageableEntity
    {
        int EntityID { get; }
        void TakeDamage(float amount);
    }

    [RequireComponent(typeof(Collider2D))]
    public class HitboxRelay : MonoBehaviour
    {
        [SerializeField] private MonoBehaviour parentEntityBehaviour;
        private IDamageableEntity targetEntity;

        public int EntityID => targetEntity != null ? targetEntity.EntityID : -1;

        private void Awake()
        {
            if (parentEntityBehaviour != null)
            {
                targetEntity = parentEntityBehaviour as IDamageableEntity;
            }

            if (targetEntity == null)
            {
                targetEntity = GetComponentInParent<IDamageableEntity>();
            }
        }

        public void ReceiveBulletHit(float damage, Vector2 hitPoint)
        {
            targetEntity?.TakeDamage(damage);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (targetEntity == null) return;

            // Supports direct trigger collisions if any non-pooled hazards enter hitbox
            if (other.TryGetComponent<HitboxRelay>(out var otherRelay) && otherRelay.EntityID != EntityID)
            {
                // Contact damage hook if needed
            }
        }
    }
}
