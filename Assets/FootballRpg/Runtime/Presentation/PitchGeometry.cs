using System;
using System.Collections.Generic;
using UnityEngine;

namespace FootballRpg.Presentation
{
    /// <summary>Metre-scaled placeholder scenery. No physics or gameplay state is advanced here.</summary>
    public sealed class PitchGeometry : IDisposable
    {
        public const float Length = 105f;
        public const float Width = 68f;
        public Transform Root { get; }
        private readonly List<Material> materials = new List<Material>();
        private readonly Material surfaceTemplate;
        private readonly Material lineTemplate;

        public PitchGeometry(Transform parent)
        {
            Root = new GameObject("Pitch Geometry").transform;
            Root.SetParent(parent, false);
            surfaceTemplate = Resources.Load<Material>("PreviewSurface");
            lineTemplate = Resources.Load<Material>("PreviewLine");
            if (surfaceTemplate == null || lineTemplate == null)
                throw new InvalidOperationException("Missing committed preview materials.");
        }

        public Material Material(Color color, bool unlit = false)
        {
            var material = new Material(unlit ? lineTemplate : surfaceTemplate) { color = color };
            materials.Add(material);
            return material;
        }

        public GameObject Shape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material material, Transform parent = null)
        {
            var shape = GameObject.CreatePrimitive(type);
            shape.name = name;
            shape.transform.SetParent(parent ?? Root, false);
            shape.transform.localPosition = position;
            shape.transform.localScale = scale;
            shape.GetComponent<Renderer>().sharedMaterial = material;
            shape.GetComponent<Collider>().enabled = false;
            return shape;
        }

        public LineRenderer Line(string name, Vector3[] points, Material material, float width = .13f, bool loop = false, Transform parent = null)
        {
            var line = new GameObject(name).AddComponent<LineRenderer>();
            line.transform.SetParent(parent ?? Root, false);
            line.useWorldSpace = false;
            line.sharedMaterial = material;
            line.widthMultiplier = width;
            line.loop = loop;
            line.positionCount = points.Length;
            line.SetPositions(points);
            return line;
        }

        public LineRenderer Circle(string name, float radius, Vector3 center, Material material, float width = .13f, Transform parent = null)
        {
            var points = new Vector3[64];
            for (int i = 0; i < points.Length; i++)
            {
                float angle = i * Mathf.PI * 2 / points.Length;
                points[i] = center + new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
            }
            return Line(name, points, material, width, true, parent);
        }

        private void Rectangle(string name, float xMin, float xMax, float zMin, float zMax, Material material)
        {
            Line(name, new[] { new Vector3(xMin, .04f, zMin), new Vector3(xMax, .04f, zMin),
                new Vector3(xMax, .04f, zMax), new Vector3(xMin, .04f, zMax) }, material, .14f, true);
        }

        public void BuildField()
        {
            var surround = Material(new Color(.065f, .13f, .14f));
            var grass = Material(new Color(.19f, .39f, .27f));
            var stripe = Material(new Color(.22f, .44f, .30f));
            var white = Material(new Color(.83f, .91f, .82f), true);
            Shape("Pitch Surround", PrimitiveType.Cube, new Vector3(0, -.22f, 0), new Vector3(125, .2f, 87), surround);
            for (int i = 0; i < 10; i++)
                Shape("Grass Stripe " + i, PrimitiveType.Cube, new Vector3(-47.25f + i * 10.5f, -.1f, 0),
                    new Vector3(10.5f, .2f, Width), i % 2 == 0 ? grass : stripe);
            Rectangle("Touchlines", -Length / 2, Length / 2, -Width / 2, Width / 2, white);
            Line("Halfway", new[] { new Vector3(0, .05f, -34), new Vector3(0, .05f, 34) }, white);
            Circle("Centre Circle", 9.15f, new Vector3(0, .05f, 0), white);
            Circle("Centre Spot", .18f, new Vector3(0, .05f, 0), white, .2f);
            foreach (int side in new[] { -1, 1 })
            {
                float goalX = side * Length / 2;
                Rectangle("Penalty Area", Mathf.Min(goalX, goalX - side * 16.5f), Mathf.Max(goalX, goalX - side * 16.5f), -20.16f, 20.16f, white);
                Rectangle("Goal Area", Mathf.Min(goalX, goalX - side * 5.5f), Mathf.Max(goalX, goalX - side * 5.5f), -9.16f, 9.16f, white);
                Circle("Penalty Spot", .18f, new Vector3(goalX - side * 11, .05f, 0), white, .2f);
                var goal = new GameObject(side < 0 ? "Home Goal" : "Away Goal").transform;
                goal.SetParent(Root, false);
                Line("Posts and Crossbar", new[] { new Vector3(goalX, 0, -3.66f), new Vector3(goalX, 2.44f, -3.66f),
                    new Vector3(goalX, 2.44f, 3.66f), new Vector3(goalX, 0, 3.66f) }, white, .18f, false, goal);
                Rectangle("Goal Base", Mathf.Min(goalX, goalX + side * 2), Mathf.Max(goalX, goalX + side * 2), -3.66f, 3.66f, white);
                for (int i = 0; i <= 8; i++)
                {
                    float z = -3.66f + i * .915f;
                    Line("Net", new[] { new Vector3(goalX, 2.44f, z), new Vector3(goalX + side * 2, 1.8f, z),
                        new Vector3(goalX + side * 2, .05f, z) }, white, .035f, false, goal);
                }
            }
            Shape("Ball", PrimitiveType.Sphere, new Vector3(0, .22f, 0), Vector3.one * .44f, white);
        }

        public void Dispose()
        {
            foreach (var material in materials) Release(material);
            if (Root != null) Release(Root.gameObject);
        }

        internal static void Release(UnityEngine.Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(obj);
            else UnityEngine.Object.DestroyImmediate(obj);
        }
    }
}
