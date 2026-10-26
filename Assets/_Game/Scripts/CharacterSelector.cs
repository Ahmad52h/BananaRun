using System;
using TMPro;
using UnityEngine;

/// <summary>Applies a saved visual variant to the existing animated monkey rig.</summary>
public sealed class CharacterSelector : MonoBehaviour
{
    private const string SelectionKey = "SelectedCharacter";

    [Serializable]
    public sealed class CharacterVariant
    {
        public string displayName = "Monkey";
        public GameObject accessoryPrefab;
        public Vector3 headOffset = new Vector3(0f, 0.39f, 0.08f);
        public Vector3 headRotation;
        public Vector3 accessoryScale = Vector3.one;
    }

    [SerializeField] private CharacterVariant[] characters =
    {
        new CharacterVariant { displayName = "Default Monkey" },
        new CharacterVariant { displayName = "Hat Monkey" }
    };
    [SerializeField] private Animator characterAnimator;
    [SerializeField] private Animator previewAnimator;
    [SerializeField] private Camera previewCamera;
    [SerializeField] private TMP_Text nameLabel;
    [SerializeField] private TMP_Text statusLabel;

    private GameObject activeAccessory;
    private GameObject previewAccessory;
    private int selectedIndex;

    public int SelectedIndex => selectedIndex;
    public int CharacterCount => characters != null ? characters.Length : 0;

    private void Awake()
    {
        if (characters == null || characters.Length == 0)
        {
            Debug.LogError("CharacterSelector needs at least one character variant.", this);
            enabled = false;
            return;
        }

        if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>(true);
        selectedIndex = Mathf.Clamp(PlayerPrefs.GetInt(SelectionKey, 0), 0, characters.Length - 1);
        ApplySelection();
        SetPreviewVisible(false);
    }

    private void Start()
    {
        if (previewAnimator == null) return;
        previewAnimator.applyRootMotion = false;
        previewAnimator.Play("Base Layer.Idle", 0, 0f);
        for (int i = 1; i < previewAnimator.layerCount; i++) previewAnimator.SetLayerWeight(i, 0f);
    }

    public void NextCharacter()
    {
        if (CharacterCount > 0) SelectCharacter((selectedIndex + 1) % CharacterCount);
    }

    public void PreviousCharacter()
    {
        if (CharacterCount > 0) SelectCharacter((selectedIndex - 1 + CharacterCount) % CharacterCount);
    }

    public void SelectCharacter(int index)
    {
        if (characters == null || index < 0 || index >= characters.Length) return;
        selectedIndex = index;
        PlayerPrefs.SetInt(SelectionKey, selectedIndex);
        PlayerPrefs.Save();
        ApplySelection();
    }

    public void SetPreviewVisible(bool visible)
    {
        if (previewCamera != null) previewCamera.enabled = visible;
    }

    private void ApplySelection()
    {
        CharacterVariant variant = characters[selectedIndex];
        ReplaceAccessory(ref activeAccessory, characterAnimator, variant);
        ReplaceAccessory(ref previewAccessory, previewAnimator, variant);

        if (nameLabel != null) nameLabel.text = variant.displayName.ToUpperInvariant();
        if (statusLabel != null)
            statusLabel.text = $"RUNNER {selectedIndex + 1} / {characters.Length}\nReady for a jungle adventure!";
    }

    private static void ReplaceAccessory(ref GameObject current, Animator animator, CharacterVariant variant)
    {
        if (current != null)
        {
            current.SetActive(false);
            Destroy(current);
            current = null;
        }
        if (animator == null || variant.accessoryPrefab == null) return;

        Transform head = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Head) : null;
        if (head == null)
        {
            foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
                if (bone.name == "mixamorig:Head" || bone.name == "Head") { head = bone; break; }
        }
        if (head == null)
        {
            Debug.LogWarning("CharacterSelector could not find the monkey head bone.", animator);
            return;
        }

        current = Instantiate(variant.accessoryPrefab, head, false);
        current.transform.localPosition = variant.headOffset;
        current.transform.localRotation = Quaternion.Euler(variant.headRotation);
        current.transform.localScale = variant.accessoryScale;
    }
}
