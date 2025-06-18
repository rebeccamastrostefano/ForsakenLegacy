using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayAnimation : MonoBehaviour
{
    Animation Animation;
  
    void Start()
    {
        Animation = gameObject.GetComponent<Animation>();
    }
    public void Play()
    {
        Animation.Play();
    }
}
