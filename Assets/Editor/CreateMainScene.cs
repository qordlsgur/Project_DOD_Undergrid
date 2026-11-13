using UnityEngine;
using UnityEditor;
using System.IO;
using UnityEditor.SceneManagement; 
using UnityEngine.SceneManagement; 
using Prohect_DOD_Undergird.Player; 

namespace Prohect_DOD_Undergird.EditorTools
{
    public static class CreateMainScene
    {
        [MenuItem("Tools/Create Main Scene")]
        private static void CreateMainSceneFromSample()
        {
            try
            {
                // 1. 원본 SampleScene 존재 여부 확인
                string sampleScenePath = "Assets/Scenes/SampleScene.unity"; 
                if (!File.Exists(sampleScenePath))
                {
                    Debug.LogError("샘플 씬 파일을 찾을 수 없습니다: " + sampleScenePath);
                    return;
                }

                // 2. Main.unity 생성 조건 검사 (기존 파일 있으면 중단)
                string newScenePath = "Assets/Scenes/Main.unity"; 
                if (File.Exists(newScenePath))
                {
                    Debug.LogWarning("Main.unity 파일이 이미 존재합니다. 덮어쓰기를 방지하기 위해 작업을 중단합니다.");
                    return;
                }

                // 3. SampleScene 복사 → Main.unity 생성
                if (!AssetDatabase.CopyAsset(sampleScenePath, newScenePath))
                {
                    Debug.LogError("SampleScene을 Main.unity로 복사하는 데 실패했습니다.");
                    return;
                }
                AssetDatabase.SaveAssets();

               
                // 4. Main 씬 열기 (Single 모드로 기존 씬 강제 대체)
                Scene mainScene = EditorSceneManager.OpenScene(newScenePath, OpenSceneMode.Single);

                if (!mainScene.isLoaded)
                {
                    Debug.LogError("Main 씬 생성 후 로딩에 실패했습니다.");
                    return;
                }

                // 5. Player GameObject 생성 및 설정
                GameObject player = new GameObject("Player"); 
                player.transform.position = Vector3.zero;

                // Rigidbody2D 추가 및 속성 설정
                Rigidbody2D rb = player.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.freezeRotation = true;

               player.AddComponent<PlayerMovement>();

                // Player를 Main 씬에 명시적으로 소속
                SceneManager.MoveGameObjectToScene(player, mainScene);

                // 추가: 모든 작업이 성공한 후 Main 씬을 명시적으로 저장
                bool saveSuccess = EditorSceneManager.SaveScene(mainScene);                
                if (saveSuccess)
                {
                    Debug.Log("Main.unity 생성 및 저장 완료.");
                }
                else
                {
                    Debug.LogError("Main.unity 생성 후 저장 실패.");
                }

            }
            catch (System.Exception e)
            {
                Debug.LogError("Main 씬 생성 중 예외 발생: " + e.Message);
            }
        }
    }
}
