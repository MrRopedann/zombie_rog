using System;
using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(AudioSource), typeof(SurfaceDetector))]
public class PlayerFootStepSounds : MonoBehaviour
{
    [System.Serializable]
    public class MovementSettings
    {
        public float moveStepInterVal = 0.5f;
        public float sprintStepInterVal = 0.3f;
        public float walkStepInterVal = 0.7f;
        public float velocityThresholder = 0.1f;
        public float landSoundVelocityThresholder = 3f;
    }

    [Header("Movement Settings")]
    [SerializeField]
    private MovementSettings movementSettings = new MovementSettings();

    [Header("Timing Settings")]
    [SerializeField]
    private float stepTimer = 0f;
    [SerializeField]
    private bool wasGroundedLastFrame = true;
    [SerializeField]
    private float currentStepInterval;

    private InputsController _inputsController;
    private CharacterController _characterController;
    private AudioSource _audioSource;
    private SurfaceDetector _surfaceDetector;
    private ThirdPersonController _thirdPersonController;

    private void Awake()
    {
        if (_characterController == null) _characterController = GetComponent<CharacterController>();
        if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
        if (_surfaceDetector == null) _surfaceDetector = GetComponent<SurfaceDetector>();
        if (_thirdPersonController == null) _thirdPersonController = GetComponent<ThirdPersonController>(); 

        _audioSource.spatialBlend = 1f;
        _audioSource.minDistance = 1f;
        _audioSource.maxDistance = 20f;

        _inputsController = GetComponent<InputsController>();
    }

    private void Update()
    {
        HandleLandingSound();
        HandleJumpSound();

        wasGroundedLastFrame = _characterController.isGrounded;

        // Шаги только когда на земле + движемся
        if (!_characterController.isGrounded)
        {
            stepTimer = 0f;
            return;
        }

        if (_characterController.velocity.magnitude < movementSettings.velocityThresholder)
        {
            stepTimer = 0f;
            return;
        }

        HandleFootsteps();
    }

    private void HandleLandingSound()
    {
        if (_characterController.isGrounded && !wasGroundedLastFrame)
        {
            float fallVelocity = Mathf.Abs(_characterController.velocity.y);
            if (fallVelocity > movementSettings.landSoundVelocityThresholder)
            {
                PlayLandSound();
            }
        }
    }
    private void HandleJumpSound()
    {
        // Проигрываем звук прыжка в момент отрыва от земли
        if (!wasGroundedLastFrame || _characterController.isGrounded)
            return;  // уже в воздухе или всё ещё на земле

        // Дополнительная проверка, что мы действительно прыгаем вверх
        if (_characterController.velocity.y > 0.1f)
        {
            PlayJumpSound();
        }
    }

    private void HandleFootsteps()
    {
        if (!_characterController.isGrounded)
            return;

        if (_characterController.velocity.magnitude < movementSettings.velocityThresholder)
        {
            stepTimer = 0f;
            return;
        }

        // Выбор интервала шага
        if (_inputsController.sprint || _characterController.velocity.magnitude > _thirdPersonController.SprintSpeed)
        {
            currentStepInterval = movementSettings.sprintStepInterVal;
        }
        else if (_inputsController.walk || _characterController.velocity.magnitude > _thirdPersonController.WalkSpeed)
        {
            currentStepInterval = movementSettings.walkStepInterVal;
        }
        else
        {
            currentStepInterval = movementSettings.moveStepInterVal;
        }

        stepTimer += Time.deltaTime;

        if (stepTimer >= currentStepInterval)
        {
            PlayerFootStepSound();
            stepTimer = 0f;
        }
    }

    private void PlayerFootStepSound()
    {
        var (surfaceTag, physicMaterial) = _surfaceDetector.GetCurrentSurfaceInfo();
        var surfaceSet = AudioManager.Instance.GetSurfaceSoundSet(surfaceTag, physicMaterial);

        AudioClip[] clipsToUse;

        if (_inputsController.sprint || _characterController.velocity.magnitude > _thirdPersonController.SprintSpeed)
        {
            clipsToUse = surfaceSet.sprintSounds.Length > 0 ? surfaceSet.sprintSounds : surfaceSet.footstepSounds;
        }
        else if (_inputsController.walk || _characterController.velocity.magnitude > _thirdPersonController.WalkSpeed)
        {
            clipsToUse = surfaceSet.walkSounds.Length > 0 ? surfaceSet.walkSounds : surfaceSet.footstepSounds;
        }
        else
        {
            clipsToUse = surfaceSet.footstepSounds;
        }

        AudioClip clip = AudioManager.Instance.GetRandomClip(clipsToUse);

        if (clip != null)
        {
            _audioSource.pitch = AudioManager.Instance.GetRandomPithc();
            _audioSource.volume = AudioManager.Instance.footstepVolume * surfaceSet.volumeMultiplier;
            _audioSource.PlayOneShot(clip);
        }

    }

    private void PlayJumpSound()
    {
        var (surfaceTag, physicMaterial) = _surfaceDetector.GetCurrentSurfaceInfo();
        var surfaceSet = AudioManager.Instance.GetSurfaceSoundSet(surfaceTag, physicMaterial);

        AudioClip clip = AudioManager.Instance.GetRandomClip(surfaceSet.jumpSounds);

        if (clip != null)
        {
            _audioSource.pitch = AudioManager.Instance.GetRandomPithc();
            _audioSource.volume = AudioManager.Instance.jumpVolume * surfaceSet.volumeMultiplier;
            _audioSource.PlayOneShot(clip);
        }
        else
        {
            Debug.LogWarning("No jump sound found for surface: " + surfaceTag);
        }
    }

    private void PlayLandSound()
    {
        var (surfaceTag, physicMaterial) = _surfaceDetector.GetCurrentSurfaceInfo();
        var surfaceSet = AudioManager.Instance.GetSurfaceSoundSet(surfaceTag, physicMaterial);

        AudioClip clip = AudioManager.Instance.GetRandomClip(surfaceSet.landSounds);

        if (clip != null)
        {
            _audioSource.pitch = AudioManager.Instance.GetRandomPithc();
            _audioSource.volume = AudioManager.Instance.landVolume * surfaceSet.volumeMultiplier;
            _audioSource.PlayOneShot(clip);
        }
    }
}
