using UnityEditor;

namespace Unity.Tutorials.Core.Editor
{
    internal class GameViewProxy : EditorWindow
    {
        public static bool maximizeOnPlay
        {
            get { return GetWindow<GameView>().enterPlayModeBehavior == PlayModeView.EnterPlayModeBehavior.PlayMaximized; }
            set { GetWindow<GameView>().enterPlayModeBehavior = value ? PlayModeView.EnterPlayModeBehavior.PlayMaximized : PlayModeView.EnterPlayModeBehavior.PlayFocused; }
        }
    }
}
