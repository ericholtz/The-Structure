using System.Collections;
using UnityEngine;
using UnityEngine.Video;

public class PlayBostonCop : MonoBehaviour
{
    public GameObject player;
    public VideoPlayer videoPlayer;
    public Camera cam;
    public GameObject flashlight;

    // Start is called before the first frame update
    private void OnTriggerEnter()
    {
        cam.GetComponent<MouseMovement>().enabled = false;
        flashlight.GetComponent<DelayedFlashlightFollow>().enabled = false;
        player.GetComponent<PlayerMovement>().enabled = false;

        StartCoroutine(DelayedPlay(1));
        StartCoroutine(StopPlaying());
    }

    IEnumerator StopPlaying()
    {
        while (videoPlayer.frame < (long)videoPlayer.frameCount - 1)
        {
            yield return null;
        }
        videoPlayer.Stop();
        cam.GetComponent<MouseMovement>().enabled = true;
        flashlight.GetComponent<DelayedFlashlightFollow>().enabled = true;
        player.GetComponent<PlayerMovement>().enabled = true;
    }

    IEnumerator DelayedPlay(int seconds)
    {
        yield return new WaitForSeconds(seconds);
        videoPlayer.Play();
    }
}
