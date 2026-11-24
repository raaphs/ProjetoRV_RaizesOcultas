using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RaizesOcultas.Editor
{
    internal sealed class RaizesProjectStartup : AssetPostprocessor
    {
        private const string ScenePath = "Assets/RaizesOcultas/Gerado/RaizesOcultas.unity";
        private const string SessionKey = "RaizesOcultas.InitialSceneOpened";

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets,
            string[] movedAssets, string[] movedFromAssetPaths, bool didDomainReload)
        {
            if (!didDomainReload || Application.isBatchMode || SessionState.GetBool(SessionKey, false))
                return;

            EditorApplication.delayCall -= OpenInitialScene;
            EditorApplication.delayCall += OpenInitialScene;
        }

        private static void OpenInitialScene()
        {
            if (SessionState.GetBool(SessionKey, false))
                return;

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall += OpenInitialScene;
                return;
            }

            SessionState.SetBool(SessionKey, true);
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                PrefabStageUtility.GetCurrentPrefabStage() != null || SceneManager.sceneCount > 1)
                return;

            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                if (SceneManager.GetSceneAt(index).isDirty)
                    return;
            }

            if (SceneManager.GetActiveScene().path == ScenePath)
                return;

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null)
            {
                Debug.LogError("A cena inicial de Raízes Ocultas não foi encontrada em " + ScenePath);
                return;
            }

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }
    }
}
