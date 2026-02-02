using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class ImageWindow : EditorWindow
{
    /*[MenuItem("Tutorials/Test Image Window")]
    public static void Test()
    {
        Open("test", new Rect(100,100, 100,100));
    }*/
    static int CurrentlyOpenCount = 0;
    static int offset = 100;
    public static ImageWindow Open(Texture2D image)
    {
        var window = CreateInstance<ImageWindow>();
        window.img = image;
        window.titleContent = new GUIContent(image.name);
        //window.maxSize = r.size;
        window.ShowUtility();
        var pos = new Rect(100 + offset * CurrentlyOpenCount, 100 + offset * CurrentlyOpenCount, image.width, image.height);
        if (pos.xMax > Screen.currentResolution.width)
        {
            pos.xMax = Screen.currentResolution.width;
            pos.xMin = pos.xMax - image.width;
        }
        if (pos.yMax > Screen.currentResolution.height)
        {
            pos.yMax = Screen.currentResolution.height;
            pos.yMin = pos.yMax - image.height;
        }

        window.position = pos;
        CurrentlyOpenCount++;
        return window;
    }
    Texture2D img;
    private void OnGUI()
    {
        GUILayout.BeginVertical();
        //img = (Texture2D)EditorGUILayout.ObjectField("Piccy", img, typeof(Texture2D), allowSceneObjects:false);
        if (img != null)
        {
            //GUI.DrawTexture(new Rect(Vector2.zero, this.position.size), img);

            var mat = new Material(Shader.Find("Sprites/Default"));
            EditorGUI.DrawPreviewTexture(new Rect(Vector2.zero, new Vector2(this.position.width, position.height)), img, mat, scaleMode: ScaleMode.ScaleAndCrop);
        }
        GUILayout.EndVertical();

        if (Event.current.type == EventType.MouseDown && Event.current.button == 0) {
            //Debug.Log(Event.current.mousePosition);
            this.Close();
        }

    }
    private void OnDestroy()
    {
        CurrentlyOpenCount--;
    }
}
