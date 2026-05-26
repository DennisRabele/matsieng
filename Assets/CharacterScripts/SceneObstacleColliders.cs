using System;
using UnityEngine;

public class SceneObstacleColliders : MonoBehaviour
{
    private static readonly string[] ObstacleNameKeywords =
    {
        "palace",
        "motebong",
        "lodge",
        "hut",
        "house",
        "tree",
        "pine",
        "conifer",
        "cypress",
        "rock"
    };

    private static readonly string[] TourMarkerNameKeywords =
    {
        "point",
        "ponit",
        "start",
        "middle",
        "last",
        "rotate"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void ConfigureSceneColliders()
    {
        EnableTerrainTreeColliders();
        MakeTourMarkerCollidersTriggers();
        AddObstacleColliders();
    }

    private static void EnableTerrainTreeColliders()
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            if (terrain == null)
                continue;

            TerrainCollider terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider == null)
                continue;

            System.Reflection.PropertyInfo property = typeof(TerrainCollider).GetProperty("enableTreeColliders");
            if (property != null && property.PropertyType == typeof(bool))
                property.SetValue(terrainCollider, true);
        }
    }

    private static void MakeTourMarkerCollidersTriggers()
    {
        Collider[] colliders = FindObjectsByType<Collider>(FindObjectsSortMode.None);
        foreach (Collider collider in colliders)
        {
            if (collider == null || collider is TerrainCollider)
                continue;

            if (IsTourMarker(collider.transform))
                collider.isTrigger = true;
        }
    }

    private static void AddObstacleColliders()
    {
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        foreach (Renderer renderer in renderers)
        {
            if (!ShouldAddObstacleCollider(renderer))
                continue;

            BoxCollider collider = renderer.gameObject.GetComponent<BoxCollider>();
            if (collider == null)
                collider = renderer.gameObject.AddComponent<BoxCollider>();

            ApplyRendererBounds(collider, renderer);
            collider.isTrigger = false;
        }
    }

    private static bool ShouldAddObstacleCollider(Renderer renderer)
    {
        if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy)
            return false;

        if (!renderer.gameObject.scene.IsValid())
            return false;

        if (renderer.GetComponent<Collider>() != null)
            return false;

        if (renderer.GetComponentInParent<CharacterScrpit>() != null)
            return false;

        if (renderer.GetComponentInParent<Camera>() != null || renderer.GetComponentInParent<Canvas>() != null)
            return false;

        if (renderer.GetComponentInParent<Terrain>() != null)
            return false;

        if (IsTourMarker(renderer.transform) || HasNameInParents(renderer.transform, "water"))
            return false;

        return HasAnyNameKeywordInParents(renderer.transform, ObstacleNameKeywords);
    }

    private static void ApplyRendererBounds(BoxCollider collider, Renderer renderer)
    {
        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh != null)
        {
            Bounds meshBounds = meshFilter.sharedMesh.bounds;
            collider.center = meshBounds.center;
            collider.size = meshBounds.size;
            return;
        }

        Bounds worldBounds = renderer.bounds;
        collider.center = renderer.transform.InverseTransformPoint(worldBounds.center);
        collider.size = AbsVector(renderer.transform.InverseTransformVector(worldBounds.size));
    }

    private static bool IsTourMarker(Transform transform)
    {
        return HasAnyNameKeywordInParents(transform, TourMarkerNameKeywords);
    }

    private static bool HasAnyNameKeywordInParents(Transform transform, string[] keywords)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            foreach (string keyword in keywords)
            {
                if (ContainsIgnoreCase(current.name, keyword))
                    return true;
            }
        }

        return false;
    }

    private static bool HasNameInParents(Transform transform, string keyword)
    {
        for (Transform current = transform; current != null; current = current.parent)
        {
            if (ContainsIgnoreCase(current.name, keyword))
                return true;
        }

        return false;
    }

    private static bool ContainsIgnoreCase(string value, string keyword)
    {
        return value != null && value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
