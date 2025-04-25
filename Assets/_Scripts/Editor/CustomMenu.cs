using UnityEditor;

namespace _Scripts.Editor
{
    public class CustomMenu
    {
        [MenuItem("三消/场景/游戏场景")]
        public static void OpenGameScene()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Scenes/InitializationScene.unity");
        }
        
        [MenuItem("三消/场景/关卡编辑器")]
        public static void OpenLevelEditor()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/_Scenes/ContentCreationScene.unity");
        }
    }
}