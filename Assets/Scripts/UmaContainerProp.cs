using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class UmaContainerProp : UmaContainer
{
    public UmaDatabaseEntry Entry;

    /// <summary> "R" or "L" while attached to a character's hand, otherwise null </summary>
    public string AttachedHand;

    /// <summary> Loaded by <see cref="PropAnimationLinks"/>, so it goes away when the animation changes </summary>
    public bool IsAutoLinked;

    public void LoadProp(UmaDatabaseEntry entry, Transform SetParent = null)
    {
        Entry = entry;
        var go = entry.Get<GameObject>();
        var prop = Instantiate(go, SetParent ? SetParent : this.transform);


        /*foreach (Renderer r in prop.GetComponentsInChildren<Renderer>())
        {
            foreach (Material m in r.sharedMaterials)
            {
                //Shaders can be differentiated by checking m.shader.name
                m.shader = Shader.Find("Unlit/Transparent Cutout");
            }
        }*/

        if (SetParent == null)
        {
            SetupHandle();
        }
    }

    /// <summary> Root handle so the prop can be moved in Pose Mode, like a character. </summary>
    private void SetupHandle()
    {
        // Props spawn at the origin, inside the character and on top of its root handle,
        // which then hides the prop and catches the clicks. Place them beside the character.
        if (Builder.CurrentUMAContainer != null)
        {
            transform.position = Builder.CurrentUMAContainer.transform.position + Vector3.right * 0.6f;
        }

        Position = transform;
        var handle = UIHandleProp.CreateAsChild(this);

        // Handle at the prop's visual center
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length > 0)
        {
            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            handle.transform.position = bounds.center;
        }
    }

    public void AttachToHand(string hand)
    {
        var character = Builder.CurrentUMAContainer;
        if (character == null)
        {
            UI.ShowMessage("Load a character first", UIMessageType.Warning);
            return;
        }

        // Props are modeled around Hand_Attach_*, so no offset is needed there
        var boneNames = new[] { $"Hand_Attach_{hand}", $"Wrist_{hand}" };
        var bones = character.GetComponentsInChildren<Transform>();
        var bone = boneNames.Select(n => bones.FirstOrDefault(b => b.name == n)).FirstOrDefault(b => b != null);
        if (bone == null)
        {
            UI.ShowMessage($"Bone {boneNames[0]} not found", UIMessageType.Warning);
            return;
        }

        transform.SetParent(bone, false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        AttachedHand = hand;
    }

    public void Detach()
    {
        transform.SetParent(null, true);
        AttachedHand = null;
        IsAutoLinked = false;
    }

    /// <summary> Remembers this prop and hand for the character's current animation. </summary>
    public void LinkToCurrentAnimation()
    {
        var animation = Builder.CurrentUMAContainer?.CurrentAnimationName;
        if (string.IsNullOrEmpty(animation))
        {
            UI.ShowMessage("Load an animation first", UIMessageType.Warning);
            return;
        }
        if (AttachedHand == null)
        {
            UI.ShowMessage("Attach the prop to a hand first", UIMessageType.Warning);
            return;
        }

        PropAnimationLinks.Set(animation, Entry.Name, AttachedHand);
        IsAutoLinked = true;
        UI.ShowMessage($"{Path.GetFileName(Entry.Name)} linked to {animation}", UIMessageType.Success);
    }

    public void UnlinkCurrentAnimation()
    {
        var animation = Builder.CurrentUMAContainer?.CurrentAnimationName;
        if (PropAnimationLinks.Remove(animation))
        {
            IsAutoLinked = false;
            UI.ShowMessage($"Link removed from {animation}", UIMessageType.Success);
        }
        else
        {
            UI.ShowMessage("The current animation has no linked prop", UIMessageType.Warning);
        }
    }

    /// <summary>
    /// Shows a text field at <paramref name="screenPosition"/> to name this prop in the prop list.
    /// The UI has no input dialog, so this reuses a copy of the prop list's search field for its style.
    /// </summary>
    public void StartRename(Vector3 screenPosition)
    {
        var template = UI.PropPageCtrl.SearchText;
        var field = Instantiate(template, UI.HandlesPanel);
        field.name = "PropRenameField";
        // Drop the search listeners copied with the template
        field.onValueChanged = new InputField.OnChangeEvent();
        field.onEndEdit = new InputField.EndEditEvent();
        field.onSubmit = new InputField.SubmitEvent();
        field.interactable = true;
        field.transform.position = screenPosition;
        field.text = PropNames.GetUserName(Entry.Name) ?? "";
        if (field.placeholder is Text placeholder)
        {
            placeholder.text = "Prop name (Enter to save)";
        }

        field.onEndEdit.AddListener(value =>
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
            {
                PropNames.Set(Entry.Name, value);
                UI.LoadPropPanel();
                UI.ShowMessage(string.IsNullOrWhiteSpace(value) ? "Prop name removed" : $"Prop named {value.Trim()}", UIMessageType.Success);
            }
            Destroy(field.gameObject);
        });
        field.Select();
        field.ActivateInputField();
    }

    /// <summary> True if the renderer belongs to a prop attached under <paramref name="owner"/> rather than to the owner itself. </summary>
    public static bool IsAttachedPropRenderer(Renderer renderer, UmaContainer owner)
    {
        var prop = renderer.GetComponentInParent<UmaContainerProp>();
        return prop != null && prop != owner;
    }
}
