using System;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ZombieHitboxBuilder
{
    public struct Settings
    {
        public float headMultiplier;
        public float torsoMultiplier;
        public float armMultiplier;
        public float legMultiplier;
        public float scale;
    }

    private const string HitboxPrefix = "Hitbox_";

    public static void EnsureHitboxes(ZombieHealth owner, Settings settings)
    {
        if (owner == null)
        {
            return;
        }

        ClearAutoHitboxes(owner);

        Animator animator = owner.GetComponentInChildren<Animator>(true);
        Transform root = owner.transform;
        EnsureAnimatorSync(owner, animator);

        Transform head = ResolveBone(animator, root, HumanBodyBones.Head, "Head");

        CreateSphereHitbox(
            owner,
            "Head",
            ZombieHitboxBodyPart.Head,
            settings.headMultiplier,
            head,
            0.24f,
            Vector3.up * 0.07f,
            settings);

        CreateRootBodyHitbox(owner, settings);

        CreateSideHitboxes(owner, animator, root, true, settings);
        CreateSideHitboxes(owner, animator, root, false, settings);
    }

    private static void ClearAutoHitboxes(ZombieHealth owner)
    {
        ZombieHitbox[] hitboxes = owner.GetComponentsInChildren<ZombieHitbox>(true);

        for (int i = 0; i < hitboxes.Length; i++)
        {
            ZombieHitbox hitbox = hitboxes[i];

            if (hitbox == null || hitbox.transform == owner.transform)
            {
                continue;
            }

            GameObject hitboxObject = hitbox.gameObject;

            if (!hitboxObject.name.StartsWith(HitboxPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            hitboxObject.SetActive(false);

            if (Application.isPlaying)
            {
                Object.Destroy(hitboxObject);
            }
            else
            {
                Object.DestroyImmediate(hitboxObject);
            }
        }
    }

    private static void CreateSideHitboxes(
        ZombieHealth owner,
        Animator animator,
        Transform root,
        bool left,
        Settings settings)
    {
        string suffix = left ? "_L" : "_R";
        string sideName = left ? "Left" : "Right";

        Transform shoulder = ResolveBone(
            animator,
            root,
            left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm,
            "UpperArm" + suffix,
            "Shoulder" + suffix,
            sideName + "UpperArm",
            sideName + "Shoulder");
        Transform hand = ResolveBone(
            animator,
            root,
            left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
            "Hand" + suffix,
            sideName + "Hand");
        Transform lowerArm = ResolveBone(
            animator,
            root,
            left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm,
            "LowerArm" + suffix,
            "ForeArm" + suffix,
            sideName + "LowerArm",
            sideName + "ForeArm");

        CreateCapsuleHitbox(
            owner,
            sideName + "Arm",
            ZombieHitboxBodyPart.Arm,
            settings.armMultiplier,
            shoulder,
            hand != null ? hand : lowerArm,
            0.12f,
            settings);

        Transform upperLeg = ResolveBone(
            animator,
            root,
            left ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg,
            "UpperLeg" + suffix,
            sideName + "UpperLeg");
        Transform foot = ResolveBone(
            animator,
            root,
            left ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot,
            "Foot" + suffix,
            "Ankle" + suffix,
            sideName + "Foot",
            sideName + "Ankle");
        Transform toes = ResolveBone(
            animator,
            root,
            left ? HumanBodyBones.LeftToes : HumanBodyBones.RightToes,
            "Toes" + suffix,
            "Toe" + suffix,
            sideName + "Toes",
            sideName + "Toe");
        Transform lowerLeg = ResolveBone(
            animator,
            root,
            left ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg,
            "LowerLeg" + suffix,
            sideName + "LowerLeg");

        CreateCapsuleHitbox(
            owner,
            sideName + "Leg",
            ZombieHitboxBodyPart.Leg,
            settings.legMultiplier,
            upperLeg,
            toes != null ? toes : foot != null ? foot : lowerLeg,
            0.14f,
            settings);
    }

    private static void CreateRootBodyHitbox(ZombieHealth owner, Settings settings)
    {
        GameObject hitboxObject = CreateHitboxObject(owner, "Body", owner.transform);
        CapsuleCollider sourceCapsule = owner.GetComponent<CapsuleCollider>();
        CapsuleCollider collider = hitboxObject.AddComponent<CapsuleCollider>();

        collider.direction = sourceCapsule != null ? sourceCapsule.direction : 1;
        collider.center = sourceCapsule != null ? sourceCapsule.center : new Vector3(0f, 0.9f, 0f);
        collider.radius = sourceCapsule != null
            ? Mathf.Max(0.01f, sourceCapsule.radius * 1.08f * settings.scale)
            : Mathf.Max(0.01f, 0.38f * settings.scale);
        collider.height = sourceCapsule != null
            ? Mathf.Max(collider.radius * 2.1f, sourceCapsule.height * 1.03f)
            : Mathf.Max(collider.radius * 2.1f, 1.85f * settings.scale);
        collider.isTrigger = true;

        ConfigureHitboxComponent(
            hitboxObject,
            owner,
            ZombieHitboxBodyPart.Torso,
            settings.torsoMultiplier,
            owner.transform,
            null,
            collider.radius,
            false);
    }

    private static void CreateSphereHitbox(
        ZombieHealth owner,
        string hitboxName,
        ZombieHitboxBodyPart bodyPart,
        float multiplier,
        Transform bone,
        float radius,
        Vector3 localOffset,
        Settings settings)
    {
        if (bone == null)
        {
            return;
        }

        GameObject hitboxObject = CreateHitboxObject(owner, hitboxName, bone);
        SphereCollider collider = hitboxObject.AddComponent<SphereCollider>();
        collider.radius = Mathf.Max(0.01f, radius * settings.scale);
        collider.isTrigger = true;

        ConfigureHitboxComponent(
            hitboxObject,
            owner,
            bodyPart,
            multiplier,
            bone,
            null,
            collider.radius,
            true,
            localOffset);
    }

    private static void CreateCapsuleHitbox(
        ZombieHealth owner,
        string hitboxName,
        ZombieHitboxBodyPart bodyPart,
        float multiplier,
        Transform start,
        Transform end,
        float radius,
        Settings settings)
    {
        if (start == null || end == null || start == end)
        {
            return;
        }

        Vector3 segment = end.position - start.position;

        if (segment.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        GameObject hitboxObject = CreateHitboxObject(owner, hitboxName, start);
        float resolvedRadius = Mathf.Max(0.01f, radius * settings.scale);
        CapsuleCollider collider = hitboxObject.AddComponent<CapsuleCollider>();
        collider.direction = 1;
        collider.center = Vector3.zero;
        collider.radius = resolvedRadius;
        collider.height = Mathf.Max(resolvedRadius * 2.1f, segment.magnitude + resolvedRadius * 2f);
        collider.isTrigger = true;

        ConfigureHitboxComponent(
            hitboxObject,
            owner,
            bodyPart,
            multiplier,
            start,
            end,
            resolvedRadius,
            false);
    }

    private static GameObject CreateHitboxObject(ZombieHealth owner, string hitboxName, Transform parent)
    {
        GameObject hitboxObject = new(HitboxPrefix + hitboxName);
        hitboxObject.layer = owner.gameObject.layer;
        hitboxObject.transform.SetParent(parent != null ? parent : owner.transform, false);
        hitboxObject.transform.localPosition = Vector3.zero;
        hitboxObject.transform.localRotation = Quaternion.identity;
        hitboxObject.transform.localScale = Vector3.one;
        return hitboxObject;
    }

    private static void EnsureAnimatorSync(ZombieHealth owner, Animator animator)
    {
        GameObject syncObject = animator != null ? animator.gameObject : owner.gameObject;
        ZombieHitboxAnimatorSync sync = syncObject.GetComponent<ZombieHitboxAnimatorSync>();

        if (sync == null)
        {
            sync = syncObject.AddComponent<ZombieHitboxAnimatorSync>();
        }

        sync.Configure(owner);
    }

    private static void ConfigureHitboxComponent(
        GameObject hitboxObject,
        ZombieHealth owner,
        ZombieHitboxBodyPart bodyPart,
        float multiplier,
        Transform start,
        Transform end,
        float radius,
        bool sphere)
    {
        ConfigureHitboxComponent(
            hitboxObject,
            owner,
            bodyPart,
            multiplier,
            start,
            end,
            radius,
            sphere,
            Vector3.zero);
    }

    private static void ConfigureHitboxComponent(
        GameObject hitboxObject,
        ZombieHealth owner,
        ZombieHitboxBodyPart bodyPart,
        float multiplier,
        Transform start,
        Transform end,
        float radius,
        bool sphere,
        Vector3 localOffset)
    {
        ZombieHitbox hitboxComponent = hitboxObject.AddComponent<ZombieHitbox>();
        hitboxComponent.Configure(
            owner,
            bodyPart,
            Mathf.Max(0f, multiplier),
            start,
            end,
            radius,
            sphere,
            false,
            localOffset);
    }

    private static Transform ResolveBone(
        Animator animator,
        Transform root,
        HumanBodyBones humanBone,
        params string[] names)
    {
        Transform bone = null;

        if (animator != null && animator.isHuman)
        {
            try
            {
                bone = animator.GetBoneTransform(humanBone);
            }
            catch (InvalidOperationException)
            {
                bone = null;
            }
        }

        return bone != null ? bone : FindChildByName(root, names);
    }

    private static Transform FindChildByName(Transform root, string[] names)
    {
        if (root == null || names == null || names.Length == 0)
        {
            return null;
        }

        Transform containsMatch = null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            for (int i = 0; i < names.Length; i++)
            {
                string targetName = names[i];

                if (string.IsNullOrEmpty(targetName))
                {
                    continue;
                }

                if (string.Equals(child.name, targetName, StringComparison.OrdinalIgnoreCase))
                {
                    return child;
                }

                if (containsMatch == null && child.name.IndexOf(targetName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    containsMatch = child;
                }
            }
        }

        return containsMatch;
    }
}
