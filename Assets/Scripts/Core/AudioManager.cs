using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [System.Serializable]
    public class SurfaceSoundSet
    {
        public string surfaceTag = "Ground";
        public AudioClip[] footstepSounds;
        public AudioClip[] jumpSounds;
        public AudioClip[] landSounds;
        public AudioClip[] sprintSounds;
        public AudioClip[] walkSounds;

        [Range(0f, 1f)]
        public float volumeMultiplier = 1f;
        public PhysicMaterial PhysicMaterial;

    }

    [Header("Settings")]
    public SurfaceSoundSet defaultSurface;
    public List<SurfaceSoundSet> surfaceSoundsSets = new List<SurfaceSoundSet>();

    [Header("Volume Settings")]
    [Range(0f,1f)]
    public float footstepVolume = 0.5f;
    [Range(0f, 1f)]
    public float jumpVolume = 0.7f;
    [Range(0f, 1f)]
    public float landVolume = 0.6f;

    [Header("Random Setings")]
    [Range(0f, 0.5f)]
    public float pitchRandomness = 0.1f;

    private Dictionary<string, SurfaceSoundSet> surfaceDictionary;
    private Dictionary<PhysicMaterial, SurfaceSoundSet> PhysicMaterialDictionary;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        { 
            Destroy(gameObject);
            return;
        }

        Instance = this;

        //DontDestroyOnLoad(gameObject);
        InitializeDictionaries();
    }

    private void InitializeDictionaries()
    {
        surfaceDictionary = new Dictionary<string, SurfaceSoundSet>();
        PhysicMaterialDictionary = new Dictionary<PhysicMaterial, SurfaceSoundSet>();

        foreach (var set in surfaceSoundsSets)
        {
            if (!string.IsNullOrEmpty(set.surfaceTag))
            { 
                surfaceDictionary[set.surfaceTag] = set;
            }

            if (set.PhysicMaterial != null)
            {
                PhysicMaterialDictionary[set.PhysicMaterial] = set;
            }
        }
    }

    public SurfaceSoundSet GetSurfaceSoundSet(string sufaceTag, PhysicMaterial physicMaterial = null)
    {
        // Приоритет: Матерьял -> Тег -> Дефолт
        if (physicMaterial != null && PhysicMaterialDictionary.ContainsKey(physicMaterial))
        {
            return PhysicMaterialDictionary[physicMaterial];
        }

        if (!string.IsNullOrEmpty(sufaceTag) && surfaceDictionary.ContainsKey(sufaceTag))
        { 
            return surfaceDictionary[sufaceTag];
        }

        return defaultSurface;
    }

    public AudioClip GetRandomClip(AudioClip[] clips)
    { 
        if(clips == null || clips.Length  == 0) return null;
        return clips[UnityEngine.Random.Range(0, clips.Length)];
    }

    public float GetRandomPithc()
    { 
        return 1f + UnityEngine.Random.Range(-pitchRandomness, pitchRandomness); 
    }
}
