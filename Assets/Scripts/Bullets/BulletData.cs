using UnityEngine;
using LetsDash.Core;

namespace LetsDash.Bullets
{
    /// <summary>
    /// Pure data container for pooled bullets. Visual GameObjects contain ONLY
    /// SpriteRenderer + BoxCollider2D (no MonoBehaviour scripts attached).
    /// </summary>
    public class BulletData
    {
        public bool isActive;
        public GameObject visualObj;
        public BulletVisualType visualType;
        public Vector2 originPos;
        public Vector2 prevPos;
        public float age_t;
        public float maxLifeTime;
        public float angle_a;
        public float speed;
        public float damage;
        public float hitRadius;
        public PathPatternType pattern;
        public LayerMask targetLayerMask;
        public int ownerEntityID;
    }
}
