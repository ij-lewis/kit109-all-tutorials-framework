using System.IO; 
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Unity.Tutorials.Core.Editor
{
    /// <summary>
    /// Contains different utilities used in Tutorials custom editors
    /// </summary>
    public static class TutorialEditorUtils
    {
        /// <summary>
        /// Same as ProjectWindowUtil.GetActiveFolderPath() but works also in 1-panel view.
        /// </summary>
        /// <returns>Returns path with forward slashes</returns>
        public static string GetActiveFolderPath()
        {
            return Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(path =>
                {
                    if (Directory.Exists(path))
                        return path;
                    if (File.Exists(path))
                        return Path.GetDirectoryName(path);
                    return path;
                })
                .FirstOrDefault()
                .AsNullIfEmpty()
                ?? "Assets";
        }

        /// <summary>
        /// Checks if a UnityEvent property is not in a specific execution state
        /// </summary>
        /// <param name="eventProperty">A property representing the UnityEvent (or derived class)</param>
        /// <param name="state"></param>
        /// <returns>True if the event is in the expected state</returns>
        internal static bool EventIsNotInState(SerializedProperty eventProperty, UnityEngine.Events.UnityEventCallState state)
        {
            SerializedProperty persistentCalls = eventProperty.FindPropertyRelative("m_PersistentCalls.m_Calls");
            for (int i = 0; i < persistentCalls.arraySize; i++)
            {
                if (persistentCalls.GetArrayElementAtIndex(i).FindPropertyRelative("m_CallState").intValue == (int)state) { continue; }
                return true;
            }
            return false;
        }

        /// <summary>
        /// Renders a warning box about the state of the event
        /// </summary>
        internal static void RenderEventStateWarning()
        {
            EditorGUILayout.HelpBox(
                Localization.Tr(
                    "One or more callbacks of this event are not using 'Editor and Runtime' mode." +
                    "If you want to be able to execute them in Edit mode as well (that is, when running tutorials out of Play mode), " +
                    "be sure to choose 'Editor and Runtime' mode. "),
                MessageType.Warning
            );
        }

        /// <summary>
        /// Opens an Url in the browser.
        /// Links to Unity's websites will open only if the user is logged in.
        /// </summary>
        /// <param name="url"></param>
        public static void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) { return; }
            string urlWithoutHttpPrefix = RemoveHttpProtocolPrefix(url);
            if (IsUnityUrlRequiringAuthentication(urlWithoutHttpPrefix) && UnityConnectProxy.loggedIn)
            {
                UnityConnectProxy.OpenAuthorizedURLInWebBrowser(url);
                return;
            }
            Application.OpenURL(url);
        }

        /// <summary>
        /// Removes "http://" and "https://" prefixes from an url
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        internal static string RemoveHttpProtocolPrefix(string url)
        {
            if (url.StartsWith("http", System.StringComparison.OrdinalIgnoreCase))
            {
                return url.Split(new string[] { "//" }, System.StringSplitOptions.None)[1];
            }
            return url;
        }

        /// <summary>
        /// Is this a Unity URL that requires Authentication?
        /// </summary>
        /// <param name="url"></param>
        /// <returns></returns>
        internal static bool IsUnityUrlRequiringAuthentication(string url)
        {
            // TODO Genesis will provide an API where we can keep a list of Unity URLs that we want to support.
            url = RemoveHttpProtocolPrefix(url);
            return url.StartsWith("unity.", System.StringComparison.OrdinalIgnoreCase) || url.Split('/')[0].ToLower().Contains(".unity.");
        }

        public static void HighlightAsset(string assetName)
        {
            if (string.IsNullOrEmpty(assetName)) { return; }

            var assets = AssetDatabase.FindAssets(assetName);
            var paths = assets.Select(a => AssetDatabase.GUIDToAssetPath(a)).ToList();

            //we dont want to ever select anything in the tutorials folder by accident
            paths.RemoveAll(p => p.Contains("Tutorial"));

            if (paths.Count > 0)
            {
                //this will also get things in the project that *start* with this name, if it appears in the list early (possible due to existing in a folder)
                //Debug.Log(string.Join("\n", paths));
                var asset = AssetDatabase.LoadMainAssetAtPath(paths.OrderBy(p => p.Length).First());
                //Selection.activeObject = asset;
                UnityEditor.EditorGUIUtility.PingObject(asset);
            }

            //Selection.activeObject = AssetDatabase.LoadMainAssetAtPath("Assets/Prefabs/" + prefabName + ".prefab");
            //Selection.gameObjects = new GameObject[] { theGameObjectIwantToSelect };
            //EditorGUIUtility.PingObject(theObjectIwantToSelect);


            //string urlWithoutHttpPrefix = RemoveHttpProtocolPrefix(url);
            //if (IsUnityUrlRequiringAuthentication(urlWithoutHttpPrefix) && UnityConnectProxy.loggedIn)
            //{
            //    UnityConnectProxy.OpenAuthorizedURLInWebBrowser(url);
            //    return;
            //}
            //Application.OpenURL(url);


        }

        public static void HighlightGameObject(string gameobjectName)
        {
            Debug.Log(gameobjectName);
            if (string.IsNullOrEmpty(gameobjectName)) { return; }

            var go = GameObject.Find(gameobjectName);

            if (go != null)
            {
                //Component bs = go.GetComponent("BasicSpawner");

                //Selection.activeObject = go;
                UnityEditor.EditorGUIUtility.PingObject(go);

                //Highlighter.Highlight("Inspector", "Scale");
            }

        }
    }
}