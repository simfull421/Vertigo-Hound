using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public sealed class ActiveRagdollController : MonoBehaviour
{
    [Header("Roots")]
    [SerializeField] private Transform animatedRoot;
    [SerializeField] private Transform ragdollRoot;

    [Header("Drive")]
    [SerializeField] private bool autoConfigureJoints = true;
    [SerializeField] private float hitSpring = 0f;
    [SerializeField] private float hitRecoveryTime = 0.5f;

    private readonly List<BoneState> bones = new();
    private readonly Dictionary<Transform, BoneState> boneLookup = new();

    private void Awake()
    {
        if (ragdollRoot == null) ragdollRoot = transform;
        if (animatedRoot == null)
        {
            Debug.LogError("[ActiveRagdollController] Animated Root가 필요합니다.");
            enabled = false;
            return;
        }
        BuildBoneCache();
    }

    private void BuildBoneCache()
    {
        bones.Clear();
        boneLookup.Clear();

        var joints = ragdollRoot.GetComponentsInChildren<ConfigurableJoint>(true);
        foreach (var joint in joints)
        {
            var ragdollBone = joint.transform;
            var animatedBone = FindAnimatedBone(ragdollBone);
            if (animatedBone == null) continue;

            var body = ragdollBone.GetComponent<Rigidbody>();
            if (body == null) continue;

            if (autoConfigureJoints)
            {
                joint.configuredInWorldSpace = false;
                joint.rotationDriveMode = RotationDriveMode.Slerp;
            }

            var state = new BoneState
            {
                ragdoll = ragdollBone,
                animated = animatedBone,
                joint = joint,
                body = body,
                ragdollStartLocal = ragdollBone.localRotation,
                animatedStartLocal = animatedBone.localRotation,
                defaultDrive = joint.slerpDrive,
                jointSpace = GetJointSpace(joint)
            };
            state.rotationOffset = state.ragdollStartLocal * Quaternion.Inverse(state.animatedStartLocal);

            bones.Add(state);
            boneLookup[ragdollBone] = state;
        }
    }

    private void FixedUpdate()
    {
        for (int i = 0; i < bones.Count; i++)
        {
            var state = bones[i];
            if (state.animated == null || state.joint == null) continue;

            Quaternion desiredLocal = state.rotationOffset * state.animated.localRotation;
            Quaternion targetRotation = Quaternion.Inverse(desiredLocal) * state.ragdollStartLocal;
            state.joint.targetRotation = Quaternion.Inverse(state.jointSpace) * targetRotation * state.jointSpace;
        }
    }

    public void ApplyHit(Transform hitBone, Vector3 force)
    {
        if (hitBone == null) return;

        foreach (var state in EnumerateAffectedBones(hitBone))
        {
            if (state.recoverRoutine != null) StopCoroutine(state.recoverRoutine);

            SetSpring(state, hitSpring);
            state.body.AddForce(force, ForceMode.VelocityChange);
            state.recoverRoutine = StartCoroutine(RecoverSpring(state));
        }
    }

    private IEnumerable<BoneState> EnumerateAffectedBones(Transform root)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            if (boneLookup.TryGetValue(t, out var state))
                yield return state;
    }

    private IEnumerator RecoverSpring(BoneState state)
    {
        float duration = Mathf.Max(0.01f, hitRecoveryTime);
        float startSpring = hitSpring;
        float endSpring = state.defaultDrive.positionSpring;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetSpring(state, Mathf.Lerp(startSpring, endSpring, t));
            yield return null;
        }

        state.joint.slerpDrive = state.defaultDrive;
        state.recoverRoutine = null;
    }

    private static void SetSpring(BoneState state, float spring)
    {
        JointDrive drive = state.joint.slerpDrive;
        drive.positionSpring = spring;
        drive.positionDamper = state.defaultDrive.positionDamper;
        drive.maximumForce = state.defaultDrive.maximumForce;
        state.joint.slerpDrive = drive;
    }

    private Transform FindAnimatedBone(Transform ragdollBone)
    {
        string path = GetRelativePath(ragdollRoot, ragdollBone);
        if (path == null) return null;
        if (path.Length == 0) return animatedRoot;
        return animatedRoot.Find(path);
    }

    private static string GetRelativePath(Transform root, Transform target)
    {
        if (root == target) return string.Empty;

        var stack = new Stack<string>();
        var current = target;
        while (current != null && current != root)
        {
            stack.Push(current.name);
            current = current.parent;
        }
        if (current != root) return null;
        return string.Join("/", stack);
    }

    private static Quaternion GetJointSpace(ConfigurableJoint joint)
    {
        Vector3 axis = joint.axis == Vector3.zero ? Vector3.right : joint.axis;
        Vector3 secondary = joint.secondaryAxis == Vector3.zero ? Vector3.up : joint.secondaryAxis;
        return Quaternion.LookRotation(axis, secondary);
    }

    private sealed class BoneState
    {
        public Transform ragdoll;
        public Transform animated;
        public ConfigurableJoint joint;
        public Rigidbody body;
        public Quaternion ragdollStartLocal;
        public Quaternion animatedStartLocal;
        public Quaternion rotationOffset;
        public Quaternion jointSpace;
        public JointDrive defaultDrive;
        public Coroutine recoverRoutine;
    }
}