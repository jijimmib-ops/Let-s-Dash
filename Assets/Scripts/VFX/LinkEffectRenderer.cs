using UnityEngine;

namespace LetsDash.VFX
{
    [RequireComponent(typeof(LineRenderer))]
    public class LinkEffectRenderer : MonoBehaviour
    {
        public enum LinkMode
        {
            Straight,
            RopeCurve,
            EnergyWave
        }

        [Header("Endpoints")]
        [SerializeField] private Transform pointA;
        [SerializeField] private Transform pointB;
        [SerializeField] private bool hideWhenEndpointInactive = true;

        [Header("Link Style & Shape")]
        [SerializeField] private LinkMode linkMode = LinkMode.RopeCurve;
        [SerializeField, Range(2, 64)] private int segmentCount = 20;
        [SerializeField] private float ropeSagAmount = 0.6f;
        [SerializeField] private float waveAmplitude = 0.15f;
        [SerializeField] private float waveFrequency = 3f;
        [SerializeField] private float waveSpeed = 8f;

        [Header("Rendering & Sorting")]
        [SerializeField] private string sortingLayerName = "Default";
        [SerializeField] private int sortingOrder = -1;
        [SerializeField] private float lineWidth = 0.08f;
        [SerializeField] private float textureScrollSpeed = 0f;

        private LineRenderer cachedLineRenderer;
        private Material runtimeMaterial;
        private Vector3[] positionBuffer;
        private Vector2 uvOffset;
        private int lastVertexCount = -1;

        public Transform PointA => pointA;
        public Transform PointB => pointB;
        public LinkMode CurrentLinkMode => linkMode;

        private void Awake()
        {
            CacheAndConfigureRenderer();
            UpdateLinkGeometry(false);
        }

        private void OnValidate()
        {
            segmentCount = Mathf.Clamp(segmentCount, 2, 64);
            lineWidth = Mathf.Max(0.001f, lineWidth);

            CacheAndConfigureRenderer();
            UpdateLinkGeometry(false);
        }

        private void LateUpdate()
        {
            if (cachedLineRenderer == null)
            {
                CacheAndConfigureRenderer();
            }

            UpdateLinkGeometry(true);
            UpdateTextureScroll();
        }

        public void SetEndpoints(Transform a, Transform b)
        {
            pointA = a;
            pointB = b;
            UpdateLinkGeometry(Application.isPlaying);
        }

        public void SetLinkMode(LinkMode mode)
        {
            linkMode = mode;
            UpdateLinkGeometry(Application.isPlaying);
        }

        private void CacheAndConfigureRenderer()
        {
            if (cachedLineRenderer == null)
            {
                cachedLineRenderer = GetComponent<LineRenderer>();
            }
            if (cachedLineRenderer == null) return;

            cachedLineRenderer.useWorldSpace = true;
            cachedLineRenderer.sortingLayerName = string.IsNullOrEmpty(sortingLayerName) ? "Default" : sortingLayerName;
            cachedLineRenderer.sortingOrder = sortingOrder;
            cachedLineRenderer.startWidth = lineWidth;
            cachedLineRenderer.endWidth = lineWidth;
        }

        private void EnsureBufferCapacity(int requiredVertices)
        {
            if (positionBuffer == null || positionBuffer.Length != requiredVertices)
            {
                positionBuffer = new Vector3[requiredVertices];
            }

            if (lastVertexCount != requiredVertices)
            {
                cachedLineRenderer.positionCount = requiredVertices;
                lastVertexCount = requiredVertices;
            }
        }

        private void UpdateLinkGeometry(bool animate)
        {
            if (cachedLineRenderer == null) return;

            bool hasEndpoints = pointA != null && pointB != null;
            bool endpointsActive = hasEndpoints &&
                (!hideWhenEndpointInactive || (pointA.gameObject.activeInHierarchy && pointB.gameObject.activeInHierarchy));

            if (!endpointsActive)
            {
                if (cachedLineRenderer.enabled)
                {
                    cachedLineRenderer.enabled = false;
                }
                return;
            }

            if (!cachedLineRenderer.enabled)
            {
                cachedLineRenderer.enabled = true;
            }

            int vertexCount = linkMode == LinkMode.Straight ? 2 : Mathf.Clamp(segmentCount, 2, 64);
            EnsureBufferCapacity(vertexCount);

            Vector3 startPos = pointA.position;
            Vector3 endPos = pointB.position;

            switch (linkMode)
            {
                case LinkMode.Straight:
                    positionBuffer[0] = startPos;
                    positionBuffer[1] = endPos;
                    break;

                case LinkMode.RopeCurve:
                    PopulateRopeCurve(startPos, endPos, vertexCount);
                    break;

                case LinkMode.EnergyWave:
                    PopulateEnergyWave(startPos, endPos, vertexCount, animate);
                    break;
            }

            cachedLineRenderer.SetPositions(positionBuffer);
        }

        private void PopulateRopeCurve(Vector3 startPos, Vector3 endPos, int vertexCount)
        {
            float distance = Vector3.Distance(startPos, endPos);
            float scaledSag = ropeSagAmount * Mathf.Min(1f, distance * 0.5f);
            Vector3 controlPoint = (startPos + endPos) * 0.5f + Vector3.down * scaledSag;

            float invSegments = 1f / (vertexCount - 1);
            for (int i = 0; i < vertexCount; i++)
            {
                float t = i * invSegments;
                float u = 1f - t;
                positionBuffer[i] = (u * u) * startPos + (2f * u * t) * controlPoint + (t * t) * endPos;
            }
        }

        private void PopulateEnergyWave(Vector3 startPos, Vector3 endPos, int vertexCount, bool animate)
        {
            Vector3 delta = endPos - startPos;
            float distance = delta.magnitude;

            if (distance <= 0.0001f)
            {
                for (int i = 0; i < vertexCount; i++)
                {
                    positionBuffer[i] = startPos;
                }
                return;
            }

            Vector3 direction = delta / distance;
            Vector3 perpendicular = new Vector3(-direction.y, direction.x, 0f);
            float phaseOffset = animate ? Time.time * waveSpeed : 0f;
            float frequencyRadians = waveFrequency * Mathf.PI * 2f;
            float invSegments = 1f / (vertexCount - 1);

            for (int i = 0; i < vertexCount; i++)
            {
                float t = i * invSegments;
                Vector3 basePos = Vector3.Lerp(startPos, endPos, t);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float offset = Mathf.Sin(t * frequencyRadians - phaseOffset) * waveAmplitude * envelope;
                positionBuffer[i] = basePos + perpendicular * offset;
            }
        }

        private void UpdateTextureScroll()
        {
            if (!Application.isPlaying || Mathf.Abs(textureScrollSpeed) <= 0.0001f) return;

            if (runtimeMaterial == null && cachedLineRenderer != null)
            {
                runtimeMaterial = cachedLineRenderer.material;
            }
            if (runtimeMaterial == null) return;

            uvOffset.x = Mathf.Repeat(uvOffset.x + textureScrollSpeed * Time.deltaTime, 1f);
            runtimeMaterial.mainTextureOffset = uvOffset;
        }
    }
}
