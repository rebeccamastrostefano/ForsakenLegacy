using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using System;

public class FollowPathToObject : MonoBehaviour
{
    // public GameObject objectToPathTo;
    private Vector3 _startPosition;
    private GameObject _objectToFollow;

    public void Follow(GameObject objectToFollow) {
        _startPosition = transform.position;
        _objectToFollow = objectToFollow;

        StartSequence();
    }

    private void StartSequence()
    {
        Sequence mySequence = DOTween.Sequence();

        if(_objectToFollow != null)
        {
            mySequence.Append(transform.DOMove(_objectToFollow.transform.position, 3));
            mySequence.Append(transform.DOMove(_startPosition, 3));
            mySequence.SetLoops(-1);
        }
        else
        {
            mySequence.Kill();
            Destroy(gameObject);
        }
    }
}
