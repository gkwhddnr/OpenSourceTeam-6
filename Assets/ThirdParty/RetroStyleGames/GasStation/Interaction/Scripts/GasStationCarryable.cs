using UnityEngine;

namespace GasStationPack.Interaction
{
    [RequireComponent(typeof(Rigidbody), typeof(BoxCollider))]
    public sealed class GasStationCarryable : MonoBehaviour
    {
        public string displayName = "Item";
        public AudioClip pickupClip;
        public AudioClip[] impactClips;
        Rigidbody body; BoxCollider shape; AudioSource audioSource;
        float lastImpact = -10f;
        public bool IsHeld { get; private set; }
        public Rigidbody Body => body ? body : body = GetComponent<Rigidbody>();
        public BoxCollider Shape => shape ? shape : shape = GetComponent<BoxCollider>();
        public float Radius => Vector3.Scale(Shape.size, transform.lossyScale).magnitude * .5f;
        // Read the physics pose, not the delayed visual pose used by interpolation.
        public Vector3 Center => Body.position + Body.rotation * Vector3.Scale(Shape.center, transform.lossyScale);
        public Vector3 RootAtCenter(Vector3 center, Quaternion rotation) => center - rotation * Vector3.Scale(Shape.center, transform.lossyScale);
        AudioSource Voice
        {
            get
            {
                if(audioSource)return audioSource;
                audioSource=gameObject.AddComponent<AudioSource>();audioSource.playOnAwake=false;audioSource.spatialBlend=1;audioSource.minDistance=.8f;audioSource.maxDistance=16;audioSource.rolloffMode=AudioRolloffMode.Linear;return audioSource;
            }
        }

        void Awake()
        {
            body=GetComponent<Rigidbody>();shape=GetComponent<BoxCollider>();
        }
        public void Grab()
        {
            if(!Body.isKinematic){Body.linearVelocity=Vector3.zero;Body.angularVelocity=Vector3.zero;}
            IsHeld=true;Body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;Body.isKinematic=true;Body.useGravity=false;
            if(pickupClip){Voice.pitch=Random.Range(.94f,1.06f);Voice.PlayOneShot(pickupClip,.28f);}
        }
        public void Release(Vector3 velocity)
        {
            IsHeld=false;Body.isKinematic=false;Body.useGravity=true;
            Body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            Body.linearVelocity=velocity;Body.angularVelocity=Random.insideUnitSphere*(velocity.sqrMagnitude>4?2f:.3f);Body.WakeUp();
        }
        void OnCollisionEnter(Collision hit)
        {
            if(IsHeld)return;
            var alarm=hit.collider.GetComponentInParent<GasStationCarAlarm>();
            if(alarm)alarm.ReportImpact(hit.relativeVelocity.magnitude,Body.mass);
            float speed=hit.relativeVelocity.magnitude;
            if(speed<.65f||Time.time-lastImpact<.15f||impactClips==null||impactClips.Length==0)return;
            lastImpact=Time.time;Voice.pitch=Random.Range(.91f,1.09f);
            Voice.PlayOneShot(impactClips[Random.Range(0,impactClips.Length)],Mathf.Clamp(speed*.075f,.05f,.65f));
        }
    }
}
