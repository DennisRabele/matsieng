using UnityEngine;

public class TourCameraMenuAudioConfig : MonoBehaviour
{
    [Header("Tour Audio")]
    public AudioClip introClip;
    public AudioClip wholeViewClip;
    public AudioClip lakeClip;
    public AudioClip villageClip;
    [Range(0f, 1f)]
    public float audioVolume = 0.75f;
    public bool loopButtonAudio = true;
}
