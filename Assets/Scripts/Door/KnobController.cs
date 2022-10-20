using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class KnobController : Interactable
{
    public bool Locked = false;
    public string KeyName = "TestKey";
    public int NoiseAmt = 20;

    private float lastTime;
    private bool isPlaying;
    public float Cooldown;

    private DoorAnimController parent;
   
    private void Start()
    {
        parent = transform.parent.parent.GetComponent<DoorAnimController>();
       
    }

    public override void InteractClick(FirstPersonController controller)
    {
        CheckAudio();
        if (Locked && !parent.open)
        {
            if (controller.Interaction.Pickup != null && //there is a pickup
                controller.Interaction.Pickup as KeyPickup != null && //it is a key
                ((KeyPickup)controller.Interaction.Pickup).KeyName == KeyName) //keys match
            {
                Debug.Log($"Opening {gameObject.name} with {KeyName}");
            }
            else
            {
                FirstPersonController.instance.UI.SetInfoText("It's Locked! There might be a key somewhere...");
                return;
            }
        }
        parent.open = !parent.open;
        LevelManager.instance.MakeNoise(transform.position, NoiseAmt);

    }
    void CheckAudio()
    {
        if (Time.time > lastTime + Cooldown)
        {
            lastTime = Time.time;
            isPlaying = true;
        }


        if (isPlaying == true)
        {
            if(parent.open)
            {
                PlayDoor();
            }
           

        }
    }
    void PlayDoor()
    {
        FindObjectOfType<AudioManager>().Play("DoorClose");
        isPlaying = false;
    }
    public void Open()
    {
        parent.open = true;
        //for enemies to open it
    }
}
