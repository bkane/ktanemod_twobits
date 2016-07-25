using UnityEngine;
using System.Collections;

public class Button : MonoBehaviour
{
    public Animator Animator;
    public KMAudio KMAudio;
    public KMSelectable Selectable { get; protected set; }

    

    void Awake()
    {
        Selectable = GetComponent<KMSelectable>();
    }

    public void Push()
    {
        Animator.SetTrigger("Push");
        KMAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
    }
}
