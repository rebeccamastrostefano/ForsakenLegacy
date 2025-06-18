using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.VisualScripting;
using UnityEngine;

public class TideAbility : MonoBehaviour
{
    public float MaxTideHeight = 6;
    private float _minTideHeight;
    public GameObject Water;
    private bool raising = false;
    public bool CanRaise { get; set; }
    public ParticleSystem Particles;

    private void Start() {
        _minTideHeight = Water.transform.position.y;
    }

    private void Update() {
        //if pressing e and can raise start tide
        if (Input.GetKeyDown(KeyCode.E) && CanRaise)
        {
            StartCoroutine(StartTide());
        }
    }
    public IEnumerator StartTide()
    {
        if(!raising)
        {  
            Particles.Play();
            gameObject.GetComponent<Animator>().SetTrigger("Pray");
            GameManager.Instance.SetAbilityState();
            raising = true;
            gameObject.transform.parent = Water.transform;
            Water.transform.DOMoveY(MaxTideHeight, 3).OnComplete(() => DecreaseTide());
            yield return new WaitForSeconds(1);
            GameManager.Instance.SetMoveState();
        }
    }

    private void DecreaseTide()
    {
        gameObject.transform.parent = null;
        Water.transform.DOMoveY(_minTideHeight, 12);
        raising = false;
    }
}
