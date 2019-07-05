using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;
using UnityEngine.UI;
using Cinemachine;
using System;
using UnityEngine.Rendering.PostProcessing;

public class WarpController : MonoBehaviour
{
    private MovementInput input;
    private Animator anim;

    public bool isLocked;

    public CinemachineFreeLook cameraFreeLook;
    private CinemachineImpulseSource impulse;
    private PostProcessVolume postVolume;
    private PostProcessProfile postProfile;

    [Space]
    public List<Transform> screenTargets = new List<Transform>();
    public Transform target;
    public float warpDuration = 0.5f;

    [Space]
    public Transform sword;
    public Transform swordHand;
    private Vector3 swordOrigRot;
    private Vector3 swordOrigPos;
    private MeshRenderer swordMesh;

    [Space]
    public Material glowMaterial;

    [Space]
    public ParticleSystem blueTrail;
    public ParticleSystem whiteTrail;
    public ParticleSystem swordParticle;

    [Space]

    [Header("Prefabs")]
    public GameObject hitParticle;

    [Space]

    [Header("Canvas")]
    public Image aim;
    public Image lockAim;
    public Vector2 uiOffset;

    // Start is called before the first frame update
    void Start()
    {
        Cursor.visible = false;

        input = GetComponent<MovementInput>();
        anim = GetComponent<Animator>();
        swordOrigPos = sword.localPosition;
        swordOrigRot = sword.localEulerAngles;
        swordMesh = sword.GetComponentInChildren<MeshRenderer>();
        swordMesh.enabled = false;

        impulse = cameraFreeLook.GetComponent<CinemachineImpulseSource>();
        postVolume = Camera.main.GetComponent<PostProcessVolume>();
        postProfile = postVolume.profile;
    }

    // Update is called once per frame
    void Update()
    {
        anim.SetFloat("Blend", input.Speed);
        UserInterface();

        if (!input.canMove)
            return;

        if (screenTargets.Count < 1)
            return;

        if (!isLocked) {
            target = screenTargets[targetIndex()];
        }

        if (Input.GetMouseButtonDown(1)) {
            LockInterface(true);
            isLocked = true;
        }

        if (Input.GetMouseButtonUp(1) && input.canMove) {
            LockInterface(false);
            isLocked = false;
        }
    
        if (!isLocked)
            return;

        if (Input.GetMouseButtonDown(0)) {
            input.RotateTowards(target);
            input.canMove = false;
            swordParticle.Play();
            swordMesh.enabled = true;
            //StartCoroutine(ShowSword());
            anim.SetTrigger("slash");
        }
    }

    private void UserInterface()
    {
        aim.transform.position = Camera.main.WorldToScreenPoint(target.position + (Vector3)uiOffset);

        if (!input.canMove)
            return;

        Color c = screenTargets.Count < 1 ? Color.clear : Color.white;
        aim.color = c;
    }

    void LockInterface(bool state)
    {
        float size = state ? 1 : 2;
        float fade = state ? 1 : 0;

        lockAim.DOFade(fade, 0.15f);
        lockAim.transform.DOScale(size, 0.15f).SetEase(Ease.OutBack);
        lockAim.transform.DORotate(Vector3.forward * 180, 0.15f, RotateMode.FastBeyond360);
        aim.transform.DORotate(Vector3.forward * 90, 0.15f, RotateMode.LocalAxisAdd);
    }
    
    void Warp()
    {
        GameObject clone = Instantiate(gameObject, transform.position, transform.rotation);
        Destroy(clone.GetComponent<WarpController>().sword.gameObject);
        Destroy(clone.GetComponent<Animator>());
        Destroy(clone.GetComponent<WarpController>());
        Destroy(clone.GetComponent<MovementInput>());
        Destroy(clone.GetComponent<CharacterController>());

        SkinnedMeshRenderer[] skinMeshList = clone.GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer smr in skinMeshList) {
            smr.material = glowMaterial;
            smr.material.DOFloat(2, "_AlphaThreshold", 5f).OnComplete(() => Destroy(clone));
        }

        ShowBody(false);
        anim.speed = 0;

        transform.DOMove(target.position, warpDuration).SetEase(Ease.InExpo).OnComplete(()=>FinishWarp());

        sword.parent = null;
        sword.DOMove(target.position, warpDuration / 1.2f);
        sword.DOLookAt(target.position, 0.2f, AxisConstraint.None);

        // Particles
        blueTrail.Play();
        whiteTrail.Play();

        // Lens Distortion
        DOVirtual.Float(0, -80, 0.2f, DistortionAmount);
        DOVirtual.Float(1, 2, 0.2f, ScaleAmount);
    }

    void FinishWarp()
    {
        ShowBody(true);

        sword.parent = swordHand;
        sword.localPosition = swordOrigPos;
        sword.localEulerAngles = swordOrigRot;

        SkinnedMeshRenderer[] skinMeshList = GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer smr in skinMeshList) {
            GlowAmount(30);
            DOVirtual.Float(30, 0, .5f, GlowAmount);
        }

        Instantiate(hitParticle, sword.position, Quaternion.identity);

        target.GetComponentInParent<Animator>().SetTrigger("hit");
        target.parent.DOMove(target.position + transform.forward, 0.5f);

        StartCoroutine(HideSword());
        StartCoroutine(PlayAnimation());
        StartCoroutine(StopParticles());

        isLocked = false;
        LockInterface(false);
        aim.color = Color.clear;

        // Camera Shake
        impulse.GenerateImpulse(Vector3.right);

        // Lens Distortion
        DOVirtual.Float(-80, 0, 0.2f, DistortionAmount);
        DOVirtual.Float(2f, 1, 0.1f, ScaleAmount);

    }

    IEnumerator PlayAnimation()
    {
        yield return new WaitForSeconds(0.2f);
        anim.speed = 1;
    }

    IEnumerator StopParticles()
    {
        yield return new WaitForSeconds(0.2f);
        blueTrail.Stop();
        whiteTrail.Stop();
    }

    IEnumerator HideSword()
    {
        yield return new WaitForSeconds(0.8f);
        swordParticle.Play();

        GameObject swordClone = Instantiate(sword.gameObject, sword.position, sword.rotation);

        swordMesh.enabled = false;

        MeshRenderer swordMR = swordClone.GetComponentInChildren<MeshRenderer>();
        Material[] materials = swordMR.materials;

        for (int i = 0; i < materials.Length; i++) {
            Material m = glowMaterial;
            materials[i] = m;
        }

        swordMR.materials = materials;
        
        for (int i = 0; i < swordMR.materials.Length; i++) {
            swordMR.materials[i].DOFloat(1, "_AlphaThreshold", 0.3f).OnComplete(() => Destroy(swordClone));
        }

        input.canMove = true;
    }

    IEnumerator ShowSword()
    {
        yield return new WaitForSeconds(0.2f);
        swordParticle.Play();

        GameObject swordClone = Instantiate(sword.gameObject, sword.position, sword.rotation);
        
        MeshRenderer swordMR = swordClone.GetComponentInChildren<MeshRenderer>();
        Material[] materials = swordMR.materials;
        
        for (int i = 0; i < materials.Length; i++) {
            Material m = glowMaterial;
            materials[i] = m;
        }

        swordMR.enabled = true;
        swordMR.materials = materials;

        for (int i = 0; i < swordMR.materials.Length; i++) {
            swordMR.materials[i].DOFloat(0, "_AlphaThreshold", 0.1f).OnComplete(() => Destroy(swordClone));
        }

        swordMesh.enabled = true;
    }

    void ShowBody(bool state)
    {
        SkinnedMeshRenderer[] skinMeshList = GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer smr in skinMeshList) {
            smr.enabled = state;
        }
    }

    void GlowAmount(float x)
    {
        SkinnedMeshRenderer[] skinMeshList = GetComponentsInChildren<SkinnedMeshRenderer>();
        foreach (SkinnedMeshRenderer smr in skinMeshList) {
            smr.material.SetVector("_FresnelAmount", new Vector4(x, x, x, x));
        }
    }

    void DistortionAmount(float x)
    {
        postProfile.GetSetting<LensDistortion>().intensity.value = x;
    }

    void ScaleAmount(float x)
    {
        postProfile.GetSetting<LensDistortion>().scale.value = x;
    }

    public int targetIndex()
    {
        float[] distances = new float[screenTargets.Count];

        for (int i = 0; i < screenTargets.Count; i++) {
            distances[i] = Vector2.Distance(Camera.main.WorldToScreenPoint(screenTargets[i].position), new Vector2(Screen.width / 2, Screen.height / 2));
        }

        float minDistance = Mathf.Min(distances);
        int index = 0;

        for (int i = 0; i < distances.Length; i++) {
            if (minDistance == distances[i])
                index = i;
        }
        
        Debug.Log("targetIndex = " + index);

        return index;
    }
}
