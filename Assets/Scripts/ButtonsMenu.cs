using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Audio;
using TMPro;

public class ButtonsMenu : MonoBehaviour
{
    public GameObject pButton, sButton, eButton, panel;
    Vector2 pbTransform, ebTransform, sbTransform, pTransform;
    Vector2 pbTransformN, ebTransformN, sbTransformN, pTransformN;
    bool settingsOpened = false;

    public TMP_Dropdown drpDown, difDown;
    public Slider musVol, sfxVol;
    public AudioMixer audMixer;

    public TMP_Text winT;

    private void Start()
    {
        /*pbTransform = pButton.transform.position;
        sbTransform = sButton.transform.position;
        ebTransform = eButton.transform.position;
        pTransform = panel.transform.position;
        pbTransformN = pbTransform;
        ebTransformN = ebTransform;
        sbTransformN = sbTransform;
        pTransformN = pTransform;*/

        if(PlayerPrefs.HasKey("Quality"))
        {
            QualitySettings.SetQualityLevel(PlayerPrefs.GetInt("Quality"));
        }
        if (PlayerPrefs.HasKey("Diffic"))
        {
            difDown.value = PlayerPrefs.GetInt("Diffic");
        }
        else { PlayerPrefs.SetInt("Diffic", 0); }

        drpDown.value = QualitySettings.GetQualityLevel();

        if (PlayerPrefs.HasKey("SfxVol")) { 
            audMixer.SetFloat("SfxVol", PlayerPrefs.GetFloat("SfxVol"));
            audMixer.SetFloat("MusicVol", PlayerPrefs.GetFloat("MusicVol")); 
        }

        float vol;
        audMixer.GetFloat("MusicVol", out vol);
        musVol.value = vol;
        audMixer.GetFloat("SfxVol", out vol);
        sfxVol.value = vol;

        if (!PlayerPrefs.HasKey("WinOrNo"))
        {
            winT.text = "F&MCs: fight and magic cards";
        }
        else if (PlayerPrefs.GetInt("WinOrNo") == 1){
            winT.text = "YOU WIN!";
        }
        else if (PlayerPrefs.GetInt("WinOrNo") == 2) {
            winT.text = "YOU LOSE!";
        }
        else if (PlayerPrefs.GetInt("WinOrNo") == 3) {
            winT.text = "DRAW!";
        }
        else { winT.text = "F&MCs: fight and magic cards"; }
        PlayerPrefs.SetInt("WinOrNo", 0);
    }

    public void Play()
    {
        SceneManager.LoadScene("SampleScene");
    }

    public void Exit()
    {
        Application.Quit();
    }

    public void Settings()
    {
        settingsOpened = !settingsOpened;
        if (settingsOpened)
        {
            pbTransformN = pbTransform + new Vector2(-400, 0);
            ebTransformN = ebTransform + new Vector2(-400, 0);
            sbTransformN = sbTransform + new Vector2(-400, 0);
            pTransformN = pTransform + new Vector2(-400, 0);
        }
        else
        {
            pbTransformN = pbTransform;
            ebTransformN = ebTransform;
            sbTransformN = sbTransform;
            pTransformN = pTransform;
        }
    }

    public void ChangeQuality()
    {
        QualitySettings.SetQualityLevel(drpDown.value);
        PlayerPrefs.SetInt("Quality", drpDown.value);
    }
    
    public void ChangeVol()
    {
        audMixer.SetFloat("SfxVol", sfxVol.value);
        audMixer.SetFloat("MusicVol", musVol.value);
        PlayerPrefs.SetFloat("SfxVol", sfxVol.value);
        PlayerPrefs.SetFloat("MusicVol", musVol.value);
    }

    public void ChangeDif()
    {
        PlayerPrefs.SetInt("Diffic", difDown.value);
    }

    private void Update()
    {
        float rast = Mathf.Sqrt((sButton.transform.position.x - sbTransformN.x) * (sButton.transform.position.x - sbTransformN.x)
            + (sButton.transform.position.y - sbTransformN.y) * (sButton.transform.position.y - sbTransformN.y));
        pButton.transform.position = Vector2.MoveTowards(pButton.transform.position, pbTransformN, Time.deltaTime * 10f * rast);
        sButton.transform.position = Vector2.MoveTowards(sButton.transform.position, sbTransformN, Time.deltaTime * 10f * rast);
        eButton.transform.position = Vector2.MoveTowards(eButton.transform.position, ebTransformN, Time.deltaTime * 10f * rast);
        panel.transform.position = Vector2.MoveTowards(panel.transform.position, pTransformN, Time.deltaTime * 10f * rast);
        if (settingsOpened)
        {
            panel.transform.localScale = new Vector2(1 - (rast / 400), 1 - (rast / 400));
        }
        else
        {
            panel.transform.localScale = new Vector2(rast / 400,rast / 400);
        }
    }
}
