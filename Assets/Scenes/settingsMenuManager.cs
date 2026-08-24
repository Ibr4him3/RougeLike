using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System;

public class settingsMenuManager : MonoBehaviour
{
    // Audio Mixer
    public AudioMixer mixer;

    // Public classes
    public Slider masterVolumeSlider;
    public Slider musicVolumeSlider;
    public Slider sfxVolumeSlider;
    public TMP_Dropdown resolutionDropdown;

    // Private Classes

    private List<Resolution> resolutions = new List<Resolution>();
    void Start()
    {
        // Call Subprograms

        BuildResolutionDropdown();

        // Add listeners
        masterVolumeSlider.onValueChanged.AddListener(SetMasterVolume);
        musicVolumeSlider.onValueChanged.AddListener(SetMusicVolume);
        sfxVolumeSlider.onValueChanged.AddListener(SetSFXVolume);
        resolutionDropdown.onValueChanged.AddListener(SetResolution);

        // Load saved settings
        LoadSettings();
    }

    void BuildResolutionDropdown()
    {
        resolutions.Clear();
        List<String> options = new List<string>();

        foreach(Resolution res in Screen.resolutions)
        {
            if(resolutions.Exists(r => r.width == res.width && r.height == res.height)) continue;

            resolutions.Add(res);
            options.Add(res.width + " x " + res.height );
        }

        resolutionDropdown.ClearOptions();
        resolutionDropdown.AddOptions(options);
    }


    void LoadSettings()
    {
        // Get saved values, or use 0 as the default
        float masterVolume = PlayerPrefs.GetFloat("masterVolume", 0f);
        float musicVolume = PlayerPrefs.GetFloat("musicVolume", 0f);
        float sfxVolume = PlayerPrefs.GetFloat("sfxVolume", 0f);

        int resolutionIndex = PlayerPrefs.GetInt("resolution");

        // Apply the saved settings
        masterVolumeSlider.value = masterVolume;
        musicVolumeSlider.value = musicVolume;
        sfxVolumeSlider.value = sfxVolume;

        SetMasterVolume(masterVolume);
        SetMusicVolume(musicVolume);
        SetSFXVolume(sfxVolume);
        SetResolution(resolutionIndex);
    }

    public void SetMasterVolume(float value)
    {
        mixer.SetFloat("masterVolume", value);

        PlayerPrefs.SetFloat("masterVolume", value);
        PlayerPrefs.Save();
    }

    public void SetMusicVolume(float value)
    {
        mixer.SetFloat("musicVolume", value);

        PlayerPrefs.SetFloat("musicVolume", value);
        PlayerPrefs.Save();
    }

    public void SetSFXVolume(float value)
    {
        mixer.SetFloat("SFXVolume", value);

        PlayerPrefs.SetFloat("sfxVolume", value);
        PlayerPrefs.Save();
    }

    public void SetResolution(int index)
    {
        Resolution res = resolutions[index];
        Screen.SetResolution(res.width, res.height, Screen.fullScreen);
        resolutionDropdown.value = index;

        PlayerPrefs.SetInt("resolution", index);
    }
}