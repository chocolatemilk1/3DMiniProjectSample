using UnityEditor;
using UnityEngine;

public static class RotorWashSetup
{
    private const string PrefabPath = "Assets/Effects/HelicopterRotorWash/HelicopterRotorWash.prefab";

    [MenuItem("Tools/Helicopter/Add Rotor Wash to Selected")]
    [MenuItem("Tools/Helicopter/Connect Blizzard to Selected Helicopter")]
    private static void AddToSelected()
    {
        GameObject helicopter = Selection.activeGameObject;
        var existing = helicopter.GetComponentInChildren<HelicopterRotorWash>(true);
        if (existing != null)
        {
            ConfigureBlizzard(existing, helicopter.transform);
            Selection.activeGameObject = existing.gameObject;
            return;
        }
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        var effect = (GameObject)PrefabUtility.InstantiatePrefab(prefab, helicopter.transform);
        Undo.RegisterCreatedObjectUndo(effect, "Add Helicopter Rotor Wash");
        effect.transform.localPosition = Vector3.zero;
        effect.transform.localRotation = Quaternion.identity;
        var wash = effect.GetComponent<HelicopterRotorWash>();
        ConfigureBlizzard(wash, helicopter.transform);
        Selection.activeGameObject = effect;
    }

    private static void ConfigureBlizzard(HelicopterRotorWash wash, Transform helicopter)
    {
        Undo.RecordObject(wash, "Connect Blizzard Rotor Wash");
        wash.blizzardOnly = true;
        wash.autoFindParticles = true;
        wash.emitDust = false;
        if (helicopter != wash.transform)
            wash.helicopterRoot = helicopter;
        if (wash.blizzardRoot == null)
        {
            Transform match = null;
            int matches = 0;
            foreach (var candidate in Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (candidate.gameObject.scene != helicopter.gameObject.scene ||
                    !HelicopterRotorWash.IsBlizzardName(candidate.name))
                    continue;
                match = candidate;
                matches++;
            }
            if (matches == 1)
                wash.blizzardRoot = match;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(wash);
        EditorUtility.SetDirty(wash);
    }

    [MenuItem("Tools/Helicopter/Add Rotor Wash to Selected", true)]
    [MenuItem("Tools/Helicopter/Connect Blizzard to Selected Helicopter", true)]
    private static bool CanAddToSelected()
    {
        return !EditorApplication.isPlayingOrWillChangePlaymode && Selection.activeGameObject != null
            && !EditorUtility.IsPersistent(Selection.activeGameObject);
    }
}
