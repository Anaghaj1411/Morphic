using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Mediapipe.Unity.Sample.HandLandmarkDetection
{
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class HandSkeletonOverlayGraphic : Graphic
    {
        private static readonly int[,] Connections =
        {
            { 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 4 },
            { 0, 5 }, { 5, 6 }, { 6, 7 }, { 7, 8 },
            { 5, 9 }, { 9, 10 }, { 10, 11 }, { 11, 12 },
            { 9, 13 }, { 13, 14 }, { 14, 15 }, { 15, 16 },
            { 13, 17 }, { 17, 18 }, { 18, 19 }, { 19, 20 },
            { 0, 17 }
        };

        [SerializeField] private UnityEngine.Color skeletonColor =
            new UnityEngine.Color(0.1f, 1f, 0.35f, 1f);
        [SerializeField, Min(1f)] private float lineWidth = 4f;
        [SerializeField, Min(1f)] private float landmarkRadius = 5f;

        private readonly Vector2[][] smoothedHands =
        {
            new Vector2[21],
            new Vector2[21]
        };
        private readonly Vector2[] pixelPoints = new Vector2[21];
        private int visibleHandCount;
        private bool hasTargets;
        private const float MissingDetectionGrace = 0.45f;
        private float missingSince = -1f;

        public void SetHands(IReadOnlyList<Vector2[]> normalizedHands, int handCount = -1)
        {
            int count = handCount >= 0
                ? handCount
                : (normalizedHands != null ? normalizedHands.Count : 0);
            if (normalizedHands == null)
                count = 0;
            else
                count = Mathf.Min(count, Mathf.Min(2, normalizedHands.Count));

            if (count <= 0)
            {
                hasTargets = false;
                if (missingSince < 0f)
                    missingSince = Time.unscaledTime;
                return;
            }

            hasTargets = true;
            missingSince = -1f;
            visibleHandCount = count;
            for (int handIndex = 0; handIndex < count; handIndex++)
            {
                Vector2[] current = normalizedHands[handIndex];
                Vector2[] dest = smoothedHands[handIndex];
                if (current == null)
                    continue;

                int points = Mathf.Min(dest.Length, current.Length);
                for (int pointIndex = 0; pointIndex < points; pointIndex++)
                    dest[pointIndex] = current[pointIndex];
            }

            SetVerticesDirty();
        }

        private void LateUpdate()
        {
            if (hasTargets || visibleHandCount <= 0)
                return;

            if (missingSince < 0f ||
                Time.unscaledTime - missingSince < MissingDetectionGrace)
                return;

            visibleHandCount = 0;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            if (visibleHandCount <= 0)
                return;

            UnityEngine.Rect bounds = GetPixelAdjustedRect();

            for (int handIndex = 0; handIndex < visibleHandCount; handIndex++)
            {
                Vector2[] hand = smoothedHands[handIndex];
                if (hand == null || hand.Length < 21)
                    continue;

                for (int i = 0; i < 21; i++)
                {
                    pixelPoints[i] = new Vector2(
                        bounds.xMin + hand[i].x * bounds.width,
                        bounds.yMax - hand[i].y * bounds.height
                    );
                }

                for (int i = 0; i < Connections.GetLength(0); i++)
                {
                    AddLine(
                        vertexHelper,
                        pixelPoints[Connections[i, 0]],
                        pixelPoints[Connections[i, 1]],
                        lineWidth
                    );
                }

                for (int i = 0; i < 21; i++)
                    AddDot(vertexHelper, pixelPoints[i], landmarkRadius);
            }
        }

        private void AddLine(VertexHelper vertexHelper, Vector2 start, Vector2 end, float width)
        {
            Vector2 direction = end - start;
            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (width * 0.5f);
            AddQuad(
                vertexHelper,
                start - normal,
                start + normal,
                end + normal,
                end - normal
            );
        }

        private void AddDot(VertexHelper vertexHelper, Vector2 center, float radius)
        {
            Vector2 extent = new Vector2(radius, radius);
            AddQuad(
                vertexHelper,
                center + new Vector2(-extent.x, -extent.y),
                center + new Vector2(-extent.x, extent.y),
                center + new Vector2(extent.x, extent.y),
                center + new Vector2(extent.x, -extent.y)
            );
        }

        private void AddQuad(VertexHelper vertexHelper, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            int startIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, a);
            AddVertex(vertexHelper, b);
            AddVertex(vertexHelper, c);
            AddVertex(vertexHelper, d);
            vertexHelper.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vertexHelper.AddTriangle(startIndex, startIndex + 2, startIndex + 3);
        }

        private void AddVertex(VertexHelper vertexHelper, Vector2 position)
        {
            vertexHelper.AddVert(
                new Vector3(position.x, position.y, 0f),
                (UnityEngine.Color32)skeletonColor,
                Vector2.zero
            );
        }
    }
}
