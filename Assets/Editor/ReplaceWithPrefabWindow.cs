#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Linq;
using System.Collections.Generic;

public class ReplaceWithPrefabWindow : EditorWindow
{
    [Header("Prefab to place")]
    public GameObject prefab;

    [Header("Options")]
    public bool keepParent = true;
    public bool keepName = true;
    public bool copyTagAndLayer = true;

    [Tooltip("Try to preserve the WORLD scale (lossyScale) of the replaced object.")]
    public bool preserveWorldScale = true;

    [Tooltip("When gathering children from a selected root, include inactive objects as well.")]
    public bool includeInactive = true;

    [Tooltip("If enabled, will iterate through all descendants of the selection and replace them too.")]
    public bool processChildrenOfSelection = false;

    [MenuItem("Tools/Replace With Prefab...")]
    public static void ShowWindow()
    {
        var w = GetWindow<ReplaceWithPrefabWindow>("Replace With Prefab");
        w.minSize = new Vector2(380, 220);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Prefab to place", EditorStyles.boldLabel);
        prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("Options", EditorStyles.boldLabel);
        keepParent = EditorGUILayout.ToggleLeft("Keep Parent", keepParent);
        keepName = EditorGUILayout.ToggleLeft("Keep Name", keepName);
        copyTagAndLayer = EditorGUILayout.ToggleLeft("Copy Tag & Layer", copyTagAndLayer);
        preserveWorldScale = EditorGUILayout.ToggleLeft("Preserve World Scale", preserveWorldScale);
        includeInactive = EditorGUILayout.ToggleLeft("Include Inactive", includeInactive);
        processChildrenOfSelection = EditorGUILayout.ToggleLeft("Process Children of Selection", processChildrenOfSelection);

        EditorGUILayout.Space(10);

        using (new EditorGUI.DisabledGroupScope(prefab == null || Selection.gameObjects.Length == 0))
        {
            if (GUILayout.Button($"Replace {GetPlural(Selection.gameObjects.Length, "Selected Object")}", GUILayout.Height(32)))
            {
                ReplaceSelected();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Select objects in the Hierarchy and assign a Prefab. The tool instantiates the prefab, transfers transform (with optional world-scale preservation) and name/tag/layer, then removes the old object. Undo is fully supported.",
            MessageType.Info);
    }

    private static string GetPlural(int n, string singular)
    {
        return n == 1 ? "1 " + singular : n + " " + singular + "s";
    }

    private void ReplaceSelected()
    {
        if (prefab == null)
        {
            EditorUtility.DisplayDialog("Replace With Prefab", "Please assign a Prefab.", "OK");
            return;
        }

        var assetType = PrefabUtility.GetPrefabAssetType(prefab);
        if (assetType == PrefabAssetType.NotAPrefab)
        {
            EditorUtility.DisplayDialog("Replace With Prefab", "Assigned object is not a Prefab Asset.", "OK");
            return;
        }

        var selection = Selection.gameObjects;

        // Build target list (optionally include all descendants)
        var targets = processChildrenOfSelection
            ? selection.SelectMany(root => root.GetComponentsInChildren<Transform>(includeInactive).Select(t => t.gameObject))
                      .Distinct()
                      .ToArray()
            : selection;

        if (targets.Length == 0)
        {
            EditorUtility.DisplayDialog("Replace With Prefab", "Nothing to replace.", "OK");
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        try
        {
            // Replace deeper children first to reduce parent/child interference
            var ordered = targets
                .OrderByDescending(t => GetHierarchyDepth(t.transform))
                .ToArray();

            var newSelection = new List<GameObject>(ordered.Length);

            foreach (var oldObj in ordered)
            {
                if (oldObj == null) continue;

                var oldTr = oldObj.transform;
                var parent = keepParent ? oldTr.parent : null;

                // Instantiate prefab into the same scene
                GameObject newObj = (GameObject)PrefabUtility.InstantiatePrefab(prefab, oldObj.scene);
                if (newObj == null)
                {
                    // Fallback to normal Instantiate if needed
                    newObj = Object.Instantiate(prefab);
                    if (newObj.scene != oldObj.scene)
                        SceneManager.MoveGameObjectToScene(newObj, oldObj.scene);
                }

                Undo.RegisterCreatedObjectUndo(newObj, "Create replacement");

                // Set parent before applying transforms to keep world pose stable if requested
                if (keepParent)
                    newObj.transform.SetParent(parent, worldPositionStays: true);

                // Transfer transform
                ApplyTransform(oldTr, newObj.transform, preserveWorldScale);

                // Transfer name
                if (keepName)
                    newObj.name = oldObj.name;

                // Transfer tag & layer
                if (copyTagAndLayer)
                {
                    newObj.tag = oldObj.tag;
                    newObj.layer = oldObj.layer;
                }

                // Keep sibling index next to original (if applicable)
                if (keepParent && parent != null)
                {
                    int sibling = oldTr.GetSiblingIndex();
                    newObj.transform.SetSiblingIndex(Mathf.Min(sibling, parent.childCount - 1));
                }

                newSelection.Add(newObj);

                // Remove old object (Undo-enabled)
                Undo.DestroyObjectImmediate(oldObj);
            }

            // Select newly created objects
            Selection.objects = newSelection.ToArray();

            // Mark scenes dirty for saving
            EditorSceneManager.MarkAllScenesDirty();

            Undo.CollapseUndoOperations(undoGroup);
        }
        catch
        {
            // Let Unity handle the undo stack on exceptions
            throw;
        }
    }

    private static int GetHierarchyDepth(Transform t)
    {
        int d = 0;
        while (t != null)
        {
            d++;
            t = t.parent;
        }
        return d;
    }

    private static void ApplyTransform(Transform from, Transform to, bool preserveWorldScale)
    {
        if (preserveWorldScale)
        {
            Vector3 worldPos = from.position;
            Quaternion worldRot = from.rotation;
            Vector3 worldScale = from.lossyScale;

            to.position = worldPos;
            to.rotation = worldRot;
            SetWorldScale(to, worldScale);
        }
        else
        {
            to.localPosition = from.localPosition;
            to.localRotation = from.localRotation;
            to.localScale = from.localScale;
        }
    }

    private static void SetWorldScale(Transform t, Vector3 desiredWorldScale)
    {
        if (t.parent == null)
        {
            t.localScale = desiredWorldScale;
            return;
        }

        Vector3 parentScale = t.parent.lossyScale;
        float sx = SafeDiv(desiredWorldScale.x, parentScale.x);
        float sy = SafeDiv(desiredWorldScale.y, parentScale.y);
        float sz = SafeDiv(desiredWorldScale.z, parentScale.z);

        t.localScale = new Vector3(sx, sy, sz);
    }

    private static float SafeDiv(float a, float b)
    {
        return Mathf.Approximately(b, 0f) ? 0f : a / b;
    }
}
#endif
