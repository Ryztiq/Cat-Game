using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Desk : Interactable
{
    public GameObject Hinge;
    public GameObject objectInsideDrawer;
    private Vector3 inDrawerPosition;

    [SerializeField] private AudioClip OpenDrawer, CloseDrawer;

    // Start is called before the first frame update
    void Start()
    {
        inDrawerPosition= transform.position;
    }

    // Update is called once per frame
    void FixedUpdate()
    {
        
        
    }

    public override void Interact(FirstPersonController controller)
    {
       
        if(!base.Open)
        { LeanTween.moveLocalZ(Hinge, 3, .5f).setEaseInOutSine(); base.Open = true; player.clip = OpenDrawer;  player.Play(); }
        else
        { LeanTween.moveLocalZ(Hinge, .3f, .5f).setEaseInOutSine(); base.Open = false; player.clip = CloseDrawer; player.Play(); }


        if (objectInsideDrawer.GetComponent<Rigidbody>().constraints != RigidbodyConstraints.FreezeAll)
        {
           DisconnectChild();
        }

    }

    private void DisconnectChild()
    {

        objectInsideDrawer.transform.parent = null;
    }


}
