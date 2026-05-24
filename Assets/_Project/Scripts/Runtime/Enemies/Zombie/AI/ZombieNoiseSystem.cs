using System;
using UnityEngine;

public static class ZombieNoiseSystem
{
    public readonly struct GunshotNoise
    {
        public GunshotNoise(Vector3 position, Transform ownerRoot, float radius, float suspicion, UnityEngine.Object source)
        {
            Position = position;
            OwnerRoot = ownerRoot;
            Radius = Mathf.Max(0f, radius);
            Suspicion = Mathf.Clamp01(suspicion);
            Source = source;
        }

        public Vector3 Position { get; }
        public Transform OwnerRoot { get; }
        public float Radius { get; }
        public float Suspicion { get; }
        public UnityEngine.Object Source { get; }
    }

    public static event Action<GunshotNoise> GunshotReported;

    public static void ReportGunshot(Vector3 position, Transform ownerRoot, float radius, float suspicion, UnityEngine.Object source = null)
    {
        if (radius <= 0f)
            return;

        GunshotReported?.Invoke(new GunshotNoise(position, ownerRoot, radius, suspicion, source));
    }
}
