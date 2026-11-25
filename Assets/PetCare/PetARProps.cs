using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PetARProps : MonoBehaviour
{
    private const int Sides = 48;
    private readonly List<Object> generated = new List<Object>();

    public static GameObject CreatePlacementMarker()
    {
        var props = new GameObject("Placement marker").AddComponent<PetARProps>();
        var ink = props.MakeMaterial(new Color32(14, 37, 55, 255), true);
        var mint = props.MakeMaterial(new Color32(62, 249, 198, 255), true);
        props.MakeShape("Outline", ink, 0.084f, 0.095f, 0f, .003f);
        props.MakeShape("Target ring", mint, 0.087f, 0.092f, .003f, .006f);
        props.MakeShape("Center dot", mint, 0f, 0.008f, .003f, .006f);
        return props.gameObject;
    }

    public static GameObject CreateFoodBowl()
    {
        var props = new GameObject("Food bowl").AddComponent<PetARProps>();
        var teal = props.MakeMaterial(new Color32(24, 134, 128, 255), false);
        var cream = props.MakeMaterial(new Color32(255, 233, 179, 255), false);
        var food = props.MakeMaterial(new Color32(155, 78, 42, 255), false);
        props.AddProfile("Ceramic bowl", teal, new[]
        {
            new Vector2(0f, .001f), new Vector2(.044f, .001f), new Vector2(.053f, .005f),
            new Vector2(.069f, .029f), new Vector2(.068f, .035f), new Vector2(.060f, .035f),
            new Vector2(.045f, .010f), new Vector2(0f, .010f)
        });
        props.MakeShape("Cream rim", cream, .060f, .068f, .032f, .037f);
        var kibble = props.AddProfile("Kibble", food, new[]
        {
            new Vector2(0f, 0f), new Vector2(.009f, 0f), new Vector2(.011f, .005f),
            new Vector2(.008f, .009f), new Vector2(0f, .010f)
        }, 8);
        kibble.transform.localPosition = Vector3.up * .012f;
        for (int i = 1; i < 9; i++)
        {
            var piece = Instantiate(kibble, props.transform, false);
            float angle = i * 2.399963f;
            float radius = .011f * Mathf.Sqrt(i);
            piece.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, .012f + (i % 3) * .002f,
                Mathf.Sin(angle) * radius);
        }
        return props.gameObject;
    }

    private Material MakeMaterial(Color color, bool bright)
    {
        var shader = bright ? Resources.Load<Shader>("PetARMarker") : Shader.Find("Standard");
        var material = new Material(shader) { color = color };
        if (!bright) material.SetFloat("_Glossiness", .35f);
        generated.Add(material);
        return material;
    }

    private void MakeShape(string label, Material material, float inner, float outer, float bottom, float top)
    {
        AddProfile(label, material, new[]
        {
            new Vector2(inner, bottom), new Vector2(outer, bottom), new Vector2(outer, top),
            new Vector2(inner, top), new Vector2(inner, bottom)
        });
    }

    private GameObject AddProfile(string label, Material material, Vector2[] profile, int sides = Sides)
    {
        var item = new GameObject(label, typeof(MeshFilter), typeof(MeshRenderer));
        item.transform.SetParent(transform, false);
        var vertices = new Vector3[sides * profile.Length];
        var triangles = new List<int>((profile.Length - 1) * sides * 6);
        for (int ring = 0; ring < profile.Length; ring++)
        {
            for (int side = 0; side < sides; side++)
            {
                float angle = side * (Mathf.PI * 2f / sides);
                int a = ring * sides + side;
                vertices[a] = new Vector3(profile[ring].x * Mathf.Cos(angle), profile[ring].y,
                    profile[ring].x * Mathf.Sin(angle));
                if (ring == profile.Length - 1) continue;
                int d = ring * sides + ((side + 1) % sides);
                triangles.Add(a);
                triangles.Add(a + sides);
                triangles.Add(d + sides);
                triangles.Add(a);
                triangles.Add(d + sides);
                triangles.Add(d);
            }
        }
        var mesh = new Mesh { name = label, vertices = vertices, triangles = triangles.ToArray() };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        generated.Add(mesh);
        item.GetComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = item.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        return item;
    }

    private void OnDestroy()
    {
        foreach (var item in generated)
        {
            if (Application.isPlaying) Destroy(item);
            else DestroyImmediate(item);
        }
    }
}
