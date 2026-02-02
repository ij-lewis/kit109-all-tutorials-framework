using System.Collections.Generic;
using System.Linq;
using Unity.Tutorials.Core.Editor;
using UnityEditor;
using UnityEngine;

/// <summary>
/// A set of common tutorial callbacks that Tutorial creators can use.
/// Callback functions need to be public instance methods.
/// </summary>
// Enable the following line temporarily if you need to create an instance of this.
//[CreateAssetMenu(fileName = "CommonTutorialCallbacksHandler", menuName = "Tutorials/CommonTutorialCallbacksHandler")]
public class CommonTutorialCallbacks : ScriptableObject 
{
    /// <summary>
    /// Mutes or unmutes the editor audio.
    /// </summary>
    /// <param name="mute">Will the editor audio be muted?</param>
    public void SetAudioMasterMute(bool mute)
    {
        EditorUtility.audioMasterMute = mute;
    }

    /// <summary>
    /// Highlights a folder/asset in the Project window.
    /// </summary>
    /// <param name="folderPath">All paths are relative to the project folder, examples:
    /// - "Assets/Hello.png"
    /// - "Packages/com.unity.somepackage/Hello.png"
    /// </param>
    public void PingFolderOrAsset(string folderPath)
    {
        // Null/empty/invalid paths are handled without problems
        EditorGUIUtility.PingObject(AssetDatabase.LoadAssetAtPath<Object>(folderPath));
    }

    /// <summary>
    /// Highlights a folder or the first asset in it, in the Project window.
    /// </summary>
    /// <param name="folderPath">All paths are relative to the project folder, examples:
    /// - "Assets/Hello.png"
    /// - "Packages/com.unity.somepackage/Hello.png"
    /// </param>
    public void PingFolderOrFirstAsset(string folderPath)
    {
        PingFolderOrAsset(GetFirstAssetPathInFolder(folderPath, true));
    }

    /// <summary>
    /// Finds a GameObject by name and sets it as the active GameObject to Selection if the GameObject is found.
    /// </summary>
    /// <param name="name"></param>
    public void SelectGameObject(string name)
    {
        var go = GameObject.Find(name);
        if (go != null)
            Selection.activeGameObject = go;
    }

    static string GetFirstAssetPathInFolder(string folder, bool includeFolders)
    {
        try
        {
            if (includeFolders)
            {
                string path = GetFirstValidAssetPath(System.IO.Directory.GetDirectories(folder));
                if (path != null)
                {
                    return path;
                }
            }

            return GetFirstValidAssetPath(System.IO.Directory.GetFiles(folder));
        }
        catch
        {
            return null;
        }
    }

    static string GetFirstValidAssetPath(string[] paths) =>
        paths.Where(path => AssetDatabase.AssetPathToGUID(path).IsNotNullOrEmpty()).FirstOrDefault();



    public bool SceneAssetExists(string name)
    {
        return 
            AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/" + name + ".unity") != null ||
            AssetDatabase.LoadAssetAtPath<SceneAsset>("Assets/Scenes/" + name + ".unity") != null;
    }


    public bool DoesGameObjectWithNameExist(string name)
    {
        return GameObject.Find(name) != null;
    }
    public bool DoesGameObjectWithNameNOTExist(string name)
    {
        return GameObject.Find(name) == null;
    }
    public static bool GameObjectWithNameExists(string name)
    {
        return GameObject.Find(name) != null;
    }
    public static GameObject GetPrefab(string name)
    {
        return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/" + name + ".prefab");
    }

    public static T PrefabComponent<T>(string name)
    {
        var go = GetPrefab(name);
        if (go == null) return default(T);

        return go.GetComponent<T>();
    }
    public static bool PrefabContainsScript<T>(string name)
    {
        var go = GetPrefab(name);
        if (go == null) return false;

        return go.GetComponent<T>() != null;
    }
    public bool DoesPrefabWithNameExist(string name)
    {
        return GetPrefab(name) != null;
    }


    public bool GameObjectIsSelected(string name)
    {
        return Selection.activeObject != null && Selection.activeObject.name.Equals(name);
    }


    public bool GameObjectsStartingWithCount(string name, int requiredCount)
    {
        return GameObjectsStartingWith(name).Count.Equals(requiredCount);
    }
    public bool GameObjectsStartingWithAtLeastCount(string name, int requiredCount)
    {
        return GameObjectsStartingWith(name).Count >= requiredCount;
    }

    static List<GameObject> tmpList = new List<GameObject>(10);
    public static List<GameObject> GameObjectsStartingWith(string name)
    {
        tmpList.Clear();
        var all = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name.StartsWith(name))
            {
                tmpList.Add(all[i]);
            }
        }
        return tmpList;
    }

    public static List<GameObject> GameObjectsContaining(string name)
    {
        tmpList.Clear();
        var all = GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name.Contains(name))
            {
                tmpList.Add(all[i]);
            }
        }
        return tmpList;
    }

    static List<Vector3> tmpListPositions = new List<Vector3>(10);
    public static bool ObjectsInDifferentLocations(List<GameObject> objects)
    {
        tmpListPositions.Clear();
        foreach (var obj in objects)
        {
            var pos = obj.transform.position;
            if (tmpListPositions.Contains(pos)) return false;

            tmpListPositions.Add(pos);
        }
        return true;
    }

    public static bool GameObjectOnLayer(string name, int layer)
    {
        var go = GameObject.Find(name);
        if (go == null) return false;

        return go.layer == layer;
    }
    public static bool PrefabOnLayer(string name, int layer)
    {
        var go = GetPrefab(name);
        if (go == null) return false;

        return go.layer == layer;
    }


    public static bool GameObjectContainsScriptByName(string script, string name) //for use when script doesnt exist in base
    {
        var go = GameObject.Find(name);
        if (go == null) return false;

        return go.GetComponent(script) != null;
    }
    public static bool GameObjectContainsScript<T>(string name)
    {
        var go = GameObject.Find(name);
        if (go == null) return false;

        return go.GetComponent<T>() != null;
    }
    public static Behaviour GameObjectComponentByName(string script, string name) //for use when script doesnt exist in base
    {
        var go = GameObject.Find(name);
        if (go == null) return default;

        var attached = go.GetComponent(script);
        if (attached is Behaviour)
            return attached as Behaviour;
        return default;
    }
    public static T GameObjectComponent<T>(string name) where T : Component
    {
        var go = GameObject.Find(name);
        if (go == null) return default(T);

        var result = go.GetComponent<T>();
        if (result == null) result = GetComponentSpecific<T>(name); //we dont do this by default because it uses FindObjectOfType which is wild expensive even in editor i assume
        return result;
    }
    //these two functions are for the common case of people having extra objects with same name but not the right component
    public static T GetComponentSpecific<T>(string goName) where T : Component
    {
        var comps = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None); 

        foreach (var comp in comps)
        {
            if (comp.gameObject.name.Equals(goName)) return comp;
        }

        return default(T);
    }
    //TODO: need to use this more next year
    public static GameObject GameObjectFindWithComponent<T>(string goName) where T : Component
    {
        var comps = FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (var comp in comps)
        {
            if (comp.gameObject.name.Equals(goName)) return comp.gameObject;
        }

        return null;
    }

    public bool CurrentSceneIs(string name)
    {
        return UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene().name.StartsWith(name);
    }


    public bool DoesScriptWithNameExist(string name)
    {
        return AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/" + name + ".cs");
    }
}
