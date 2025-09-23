using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Unified Sound Manager allowing playback by name.
/// Supports one background music AudioSource and pooled SFX AudioSources.
/// Assign sound entries in inspector, then call PlaySound("name").
/// </summary>

[DefaultExecutionOrder(100)]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    [Serializable]
    public class SoundEntry
    {
        [Tooltip("Unique identifier for this sound")]
        public string name;
        [Tooltip("Audio clip to play")]     
        public AudioClip clip;
        [Tooltip("Volume (0-1)")]
        [Range(0f,1f)] public float volume = 1f;
    }
    
    [Header("Base Multipliers")]
    [SerializeField, Range(0f, 1f)] private float defaultSfxVolumeMultiplier = 1f;
    [SerializeField, Range(0f, 1f)] private float defaultMusicVolumeMultiplier = 1f;


    [Header("Sound Library")]
    [Tooltip("List of named sounds")]
    [SerializeField] private SoundEntry[] soundEntries;
    
    [Header("BG Music library")]
    [Tooltip("List of bg music files")]
    [SerializeField] private SoundEntry[] bgMusicEntries;

    [Header("Music Settings")]
    [Tooltip("AudioSource for background music")]    
    public AudioSource musicSource;

    [Header("SFX Pool Settings")]
    [Tooltip("Initial number of pooled SFX AudioSources")]
    [SerializeField] private int initialSfxPoolSize = 10;
    private List<AudioSource> sfxPool;

    // Internal  sound lookup dictionary
    private Dictionary<string, SoundEntry> soundDict;
    // Internal  bg music lookup dictionary
    private Dictionary<string, SoundEntry> bgMusicDict;
    // Sounds that already have been played
    private HashSet<string> playingOnce = new HashSet<string>();
    
    private int audioSourceCounter = 0;


    void Awake()
    {
        // Singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Build sound lookup
        soundDict = new Dictionary<string, SoundEntry>(soundEntries.Length);
        foreach (var entry in soundEntries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.name) || entry.clip == null)
                continue;
            soundDict[entry.name] = entry;
        }
        // Build bg music lookup
        bgMusicDict = new Dictionary<string, SoundEntry>(bgMusicEntries.Length);
        foreach (var entry in bgMusicEntries)
        {
            if (entry == null || string.IsNullOrEmpty(entry.name) || entry.clip == null)
                continue;
            bgMusicDict[entry.name] = entry;
        }

        // Initialize SFX pool
        sfxPool = new List<AudioSource>(initialSfxPoolSize);
        ExpandSfxPool(initialSfxPoolSize);

    }

    private void Start()
    {
        PlayMusic("MeadowLvl", true);
        ApplySavedVolumes(); 
    }

    /// <summary>
    /// Play a named sound as one-shot SFX.
    /// </summary>
    public AudioClip PlaySound(string soundName)
    {
        if (!soundDict.TryGetValue(soundName, out var entry))
        {
            Debug.LogWarning($"Sound '{soundName}' not found in SoundManager library.");
            return null;
        }

        var src = GetAvailableSfxSource();
        src.spatialBlend = 0f;
        
        float master = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float sfxVol = PlayerPrefs.GetFloat("SfxVolume", 1f);
        float volumeScale = master * sfxVol * defaultSfxVolumeMultiplier * entry.volume;

        src.PlayOneShot(entry.clip, volumeScale);
        return entry.clip;
    }
    

    /// <summary>
    /// Plays the sound only once at a time. Allows re-playing after the clip finishes.
    /// </summary>
    public AudioClip PlaySoundOnceUntilComplete(string soundName)
    {
        if (playingOnce.Contains(soundName))
            return null;

        if (!soundDict.TryGetValue(soundName, out var entry))
        {
            Debug.LogWarning($"Sound '{soundName}' not found in SoundManager library.");
            return null;
        }

        // Find available audio source or expand pool
        var src = GetAvailableSfxSource();
        src.spatialBlend = 0f;

        float master = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float sfxVol = PlayerPrefs.GetFloat("SfxVolume", 1f);
        float volumeScale = master * sfxVol * defaultSfxVolumeMultiplier * entry.volume;

        src.PlayOneShot(entry.clip, volumeScale);

        // Track as "currently playing"
        playingOnce.Add(soundName);
        RemoveFromOnceAfterDelay(soundName, entry.clip.length).Forget();

        return entry.clip;
    }
    
    /// <summary>
    /// Plays a named sound at a specific world position using pooled AudioSource.
    /// </summary>
    public AudioClip PlaySoundAtPosition(string soundName, Vector3 position)
    {
        if (!soundDict.TryGetValue(soundName, out var entry))
        {
            Debug.LogWarning($"Sound '{soundName}' not found in SoundManager library.");
            return null;
        }

        var src = GetAvailableSfxSource();
        src.spatialBlend = 1f;
        src.transform.position = position;

        float master = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float sfxVol = PlayerPrefs.GetFloat("SfxVolume", 1f);
        float volumeScale = master * sfxVol * defaultSfxVolumeMultiplier * entry.volume;

        src.PlayOneShot(entry.clip, volumeScale);
        return entry.clip;
    }

    /// <summary>
    /// Plays a named sound once at a world position. Prevents replaying until finished. Uses pooled AudioSource.
    /// </summary>
    public AudioClip PlaySoundOnceAtPositionUntilComplete(string soundName, Vector3 position)
    {
        if (playingOnce.Contains(soundName))
            return null;

        if (!soundDict.TryGetValue(soundName, out var entry))
        {
            Debug.LogWarning($"Sound '{soundName}' not found in SoundManager library.");
            return null;
        }

        var src = GetAvailableSfxSource();
        src.spatialBlend = 1f;
        src.transform.position = position;

        float master = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float sfxVol = PlayerPrefs.GetFloat("SfxVolume", 1f);
        float volumeScale = master * sfxVol * defaultSfxVolumeMultiplier * entry.volume;

        src.PlayOneShot(entry.clip, volumeScale);

        playingOnce.Add(soundName);
        RemoveFromOnceAfterDelay(soundName, entry.clip.length).Forget();

        return entry.clip;
    }


    
    private async UniTaskVoid RemoveFromOnceAfterDelay(string soundName, float delay)
    {
        await UniTask.Delay(System.TimeSpan.FromSeconds(delay), cancellationToken: this.GetCancellationTokenOnDestroy());
        playingOnce.Remove(soundName);
    }



    /// <summary>
    /// Play background music clip.
    /// </summary>
    public void PlayMusic(string soundName, bool loop = true)
    {
        if (musicSource == null)
            return;

        if (!bgMusicDict.TryGetValue(soundName, out var entry))
        {
            Debug.LogWarning($"Music '{soundName}' not found in SoundManager library.");
            return;
        }

        float master = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float musicVol = PlayerPrefs.GetFloat("MusicVolume", 1f);
        float finalVolume = master * musicVol * defaultMusicVolumeMultiplier * entry.volume;

        musicSource.clip = entry.clip;
        musicSource.loop = loop;
        musicSource.volume = finalVolume;
        musicSource.Play();
    }

    
    public void ApplySavedVolumes()
    {
        float master = PlayerPrefs.GetFloat("MasterVolume", 1f);
        float music = PlayerPrefs.GetFloat("MusicVolume", 1f);

        if (musicSource != null && musicSource.clip != null)
        {
            foreach (var entry in bgMusicEntries)
            {
                if (entry.clip == musicSource.clip)
                {
                    musicSource.volume = master * music * defaultMusicVolumeMultiplier * entry.volume;
                    break;
                }
            }
        }
    }


    /// <summary>
    /// Stop background music immediately.
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    /// <summary>
    /// Expands the SFX pool by count and returns new sources.
    /// </summary>
    private List<AudioSource> ExpandSfxPool(int count)
    {
        var newList = new List<AudioSource>(count);
        for (int i = 0; i < count; i++)
        {
            GameObject go = new GameObject($"SFX_AudioSource_{audioSourceCounter++}");
            go.transform.parent = this.transform;

            AudioSource src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = false;
            src.minDistance = 1f;
            src.maxDistance = 20f;
            src.rolloffMode = AudioRolloffMode.Linear;

            sfxPool.Add(src);
            newList.Add(src);
        }
        return newList;
    }
    
    private AudioSource GetAvailableSfxSource()
    {
        return sfxPool.Find(s => !s.isPlaying) ?? ExpandSfxPool(1)[0];
    }


    
    public List<AudioSource> GetAllSfxSources()
    {
        return sfxPool;
    }

}
