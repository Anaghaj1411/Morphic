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

        private IReadOnlyList<Vector2[]> hands = System.Array.Empty<Vector2[]>();
        private List<Vector2[]> smoothedHands = new List<Vector2[]>();
        private const float MissingDetectionGrace = 0.12f;
        private float missingDetectionTime;
        private float lastHandSampleTime;

        public void SetHands(IReadOnlyList<Vector2[]> normalizedHands)
        {
            if (normalizedHands == null || normalizedHands.Count == 0)
            {
                missingDetectionTime += Time.unscaledDeltaTime;
                if (missingDetectionTime < MissingDetectionGrace)
                {
                    return;
                }

                smoothedHands.Clear();
                hands = System.Array.Empty<Vector2[]>();
                SetVerticesDirty();
                return;
            }

            missingDetectionTime = 0f;

            float now = Time.unscaledTime;
            float dt = lastHandSampleTime > 0f
                ? Mathf.Clamp(now - lastHandSampleTime, 1f / 120f, 0.25f)
                : 1f / 30f;
            lastHandSampleTime = now;

            if (smoothedHands.Count != normalizedHands.Count)
            {
                smoothedHands.Clear();
                for (int handIndex = 0; handIndex < normalizedHands.Count; handIndex++)
                {
                    smoothedHands.Add((Vector2[])normalizedHands[handIndex].Clone());
                }
            }
            else
            {
                for (int handIndex = 0; handIndex < normalizedHands.Count; handIndex++)
                {
                    Vector2[] previous = smoothedHands[handIndex];
                    Vector2[] current = normalizedHands[handIndex];
                    if (previous.Length != current.Length)
                    {
                        smoothedHands[handIndex] = (Vector2[])current.Clone();
                        continue;
                    }

                    for (int pointIndex = 0; pointIndex < current.Length; pointIndex++)
                    {
                        Vector2 difference = current[pointIndex] - previous[pointIndex];
                        float distance = difference.magnitude;
                        if (distance <= 0.0015f)
                        {
                            continue;
                        }

                        float movementSpeed = distance / dt;
                        float adaptiveSpeed = 32f + Mathf.Clamp(movementSpeed, 0f, 3f) * 2f;
                        float smoothing = 1f - Mathf.Exp(-adaptiveSpeed * dt);

                        // Let real hand movement through gradually while
                        // damping isolated landmark jumps.
                        if (distance > 0.12f)
                        {
                            smoothing *= 0.75f;
                        }

                        previous[pointIndex] += difference * smoothing;
                    }
                }
            }

            hands = smoothedHands;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertexHelper)
        {
            vertexHelper.Clear();

            if (hands == null || hands.Count == 0)
            {
                return;
            }

            UnityEngine.Rect bounds = GetPixelAdjustedRect();

            foreach (Vector2[] hand in hands)
            {
                if (hand == null || hand.Length < 21)
                {
                    continue;
                }

                Vector2[] points = new Vector2[hand.Length];
                for (int i = 0; i < hand.Length; i++)
                {
                    points[i] = new Vector2(
                        bounds.xMin + hand[i].x * bounds.width,
                        bounds.yMax - hand[i].y * bounds.height
                    );
                }

                for (int i = 0; i < Connections.GetLength(0); i++)
                {
                    AddLine(
                        vertexHelper,
                        points[Connections[i, 0]],
                        points[Connections[i, 1]],
                        lineWidth
                    );
                }

                for (int i = 0; i < 21; i++)
                {
                    AddCircle(vertexHelper, points[i], landmarkRadius, 12);
                }
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

        private void AddCircle(VertexHelper vertexHelper, Vector2 center, float radius, int segments)
        {
            int centerIndex = vertexHelper.currentVertCount;
            AddVertex(vertexHelper, center);

            for (int i = 0; i < segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                AddVertex(
                    vertexHelper,
                    center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius
                );
            }

            for (int i = 0; i < segments; i++)
            {
                int current = centerIndex + 1 + i;
                int next = centerIndex + 1 + ((i + 1) % segments);
                vertexHelper.AddTriangle(centerIndex, current, next);
            }
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
