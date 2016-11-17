using UnityEngine;
using System.Collections;

public class Button : MonoBehaviour
{
    public Animator Animator;
    public KMAudio KMAudio;
    public KMSelectable Selectable;

    public void Push()
    {
        Animator.Play("button_push", -1, 0);
        KMAudio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, transform);
        Selectable.AddInteractionPunch();
    }
}
