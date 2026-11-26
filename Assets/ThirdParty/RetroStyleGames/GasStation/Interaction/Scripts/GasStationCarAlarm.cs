using UnityEngine;

namespace GasStationPack.Interaction
{
    public sealed class GasStationCarAlarm : MonoBehaviour
    {
        public AudioSource siren;
        public AudioClip alarmClip;
        public Light[] headlights;
        public Renderer[] lampLenses;
        public float minimumSpeed=3f,minimumEnergy=2f,duration=14f,flashInterval=.42f;
        [Range(0,1)] public float sirenVolume=.32f;
        float until,lastTrigger=-100; bool lit;
        public bool IsActive=>Time.time<until;
        public bool LampsOn=>lit;
        void Awake()
        {
            if(!siren)siren=GetComponent<AudioSource>();
            if(siren){siren.clip=alarmClip;siren.loop=true;siren.playOnAwake=false;siren.spatialBlend=1;siren.minDistance=3;siren.maxDistance=48;siren.rolloffMode=AudioRolloffMode.Linear;siren.volume=0;siren.Stop();}
            SetLamps(false);
        }
        public bool ReportImpact(float speed,float mass)
        {
            if(speed<minimumSpeed||.5f*Mathf.Max(.01f,mass)*speed*speed<minimumEnergy||Time.time-lastTrigger<.7f)return false;
            lastTrigger=Time.time;until=Time.time+duration;
            if(siren&&alarmClip&&!siren.isPlaying){siren.volume=0;siren.Play();}SetLamps(true);return true;
        }
        void Update()
        {
            if(IsActive)
            {
                SetLamps(Mathf.Repeat(Time.time-lastTrigger,flashInterval*2)<flashInterval);
                if(siren)siren.volume=sirenVolume*Mathf.Clamp01((Time.time-lastTrigger)/.06f)*Mathf.Clamp01((until-Time.time)/.24f);
            }
            else{SetLamps(false);if(siren&&siren.isPlaying)siren.Stop();}
        }
        void SetLamps(bool value)
        {
            lit=value;if(headlights!=null)foreach(var lamp in headlights)if(lamp)lamp.enabled=value;
            if(lampLenses!=null)foreach(var lens in lampLenses)if(lens)
            {
                lens.enabled=value;
            }
        }
        void OnDisable(){until=0;SetLamps(false);if(siren)siren.Stop();}
    }
}
