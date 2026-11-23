using System.IO;
using OpenSourceTeam6.UI.Unity;
using UnityEditor;
using UnityEngine;

namespace OpenSourceTeam6.UI.Editor
{
    public static class FirstLaunchOnboardingMenu
    {
        [MenuItem("OpenSourceTeam6/UI/최초 실행 인트로·이력서 가이드 기록 초기화")]
        public static void ResetFirstLaunchOnboardingState()
        {
            string path = FirstLaunchOnboardingController.StateFilePath;
            FirstLaunchOnboardingController.ResetSavedState();
            Debug.Log("[FirstLaunchOnboarding] 최초 실행 기록을 초기화했습니다. 다음 Play 시 오프닝 문구와 이력서 가이드가 다시 표시됩니다.\n경로: " + path);
        }

        [MenuItem("OpenSourceTeam6/UI/최초 실행 기록 폴더 열기")]
        public static void RevealFirstLaunchStateFolder()
        {
            string dir = Path.GetDirectoryName(FirstLaunchOnboardingController.StateFilePath);
            if (!string.IsNullOrEmpty(dir))
            {
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                EditorUtility.RevealInFinder(dir);
            }
        }
    }
}
