using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu_Start : MonoBehaviour
{
    // Start is called before the first frame update
    //private Vector3 LoadPosition = new Vector3(-1.4104867f, 3.72687531f, 5.35491753f);

    private void Start()
    {
        
    }
    public void OnMouseDown()
    {
        Vector3 cameraPos = Camera.main.transform.position;

        if ((cameraPos - transform.position).magnitude < 1.5)
        {
            this.GetComponent<Collider>().enabled = false;
            GameManager.instance.LoadScene("Granny's House");
        }
    }

    
}
