using UnityEditor;
using UnityEngine;
using System.IO;
using OpenSourceTeam6.Time.Unity;
[InitializeOnLoad]
public static class TimeSceneVerification {
 static double next; static int index;
 static readonly float[] hours={5.5f,6.5f,12f,18f,19f,22f};
 static TimeSceneVerification(){EditorApplication.update+=Start;EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode && File.Exists("Temp/verify-time-scene.request"))EditorApplication.update+=Start;};}
 static void Start(){
 EditorApplication.update-=Start;
 Debug.Log("TIME_VERIFY_START cwd="+System.Environment.CurrentDirectory+" playing="+EditorApplication.isPlaying+" request="+File.Exists("Temp/verify-time-scene.request"));
 if(File.Exists("Temp/create-time-scene.request")){EditorApplication.update+=Start;return;}
 if(File.Exists("Temp/verify-time-scene.request")){
 File.Delete("Temp/verify-time-scene.request");SessionState.SetBool("TimeVerify",true);if(!EditorApplication.isPlaying)EditorApplication.isPlaying=true;}
 if(SessionState.GetBool("TimeVerify",false)) {next=EditorApplication.timeSinceStartup+4;EditorApplication.update+=Tick;}
 }
 static void Tick(){
 if(!EditorApplication.isPlaying)return;
 EditorApplication.QueuePlayerLoopUpdate();
 if(EditorApplication.timeSinceStartup<next)return;
 var time=Object.FindAnyObjectByType<BusinessTimeController>();var demo=Object.FindAnyObjectByType<TimeDemoPanel>();var cam=Camera.main;
 if(!time || !demo || !cam){Finish();return;}
 demo.enabled=false;
 if(index>=hours.Length*2){Finish();return;}
 float hour=hours[index/2];
 if(index%2==0){
 if(time.Clock.Phase==OpenSourceTeam6.Time.Core.BusinessPhase.Ready)time.BeginDay(1);
 time.ApplyExternalProgress(time.Clock.DayId,hour/24f);
 cam.transform.rotation=hour<6?Quaternion.Euler(-12,90,0):Quaternion.Euler(-12,hour<17?90:270,0);
 if(hour==12)cam.transform.rotation=Quaternion.Euler(-75,90,0);
 if(hour>=19){var moon=GameObject.Find("Moon visual");if(moon)cam.transform.LookAt(moon.transform);}
 }else{
 Directory.CreateDirectory("output/time-verification");
 if(index==1)File.WriteAllText("output/time-verification/results.txt", "");
 File.AppendAllText("output/time-verification/results.txt",$"hour={time.GameHour} cloudRotation={RenderSettings.skybox.GetFloat("_Rotation")}\n");
 var target=new RenderTexture(1280,720,24); var previous=cam.targetTexture;var active=RenderTexture.active;
 try{cam.targetTexture=target;cam.Render();RenderTexture.active=target;
 var image=new Texture2D(1280,720,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes("output/time-verification/hour-"+hour.ToString("00.0",System.Globalization.CultureInfo.InvariantCulture)+".png",image.EncodeToPNG());Object.Destroy(image);
 var bodies=Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
 foreach(var b in bodies)if(b.name=="Sun visual" || b.name=="Moon visual")File.AppendAllText("output/time-verification/results.txt",hour+" "+b.name+" active="+b.gameObject.activeSelf+" height="+(b.transform.position.y-cam.transform.position.y)+"\n");
 }finally{cam.targetTexture=previous;RenderTexture.active=active;target.Release();Object.Destroy(target);}
 }
 index++;next=EditorApplication.timeSinceStartup+1;
 }
 static void Finish(){SessionState.SetBool("TimeVerify",false);EditorApplication.update-=Tick;EditorApplication.isPlaying=false;Debug.Log("TIME_VISUAL_VERIFICATION_FINISHED");}
}
