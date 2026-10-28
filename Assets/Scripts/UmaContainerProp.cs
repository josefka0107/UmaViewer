using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class UmaContainerProp : UmaContainer
{
    public void LoadProp(UmaDatabaseEntry entry, Transform SetParent = null)
    {
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
        var handle = UIHandleProp.CreateAsChild(this,
            () => AttachToCharacter("Hand_Attach_R", "Wrist_R"),
            () => AttachToCharacter("Hand_Attach_L", "Wrist_L"),
            Detach);

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

    private void AttachToCharacter(params string[] boneNames)
    {
        var character = Builder.CurrentUMAContainer;
        if (character == null)
        {
            UI.ShowMessage("Load a character first", UIMessageType.Warning);
            return;
        }

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
    }

    private void Detach()
    {
        transform.SetParent(null, true);
    }

    /// <summary> True if the renderer belongs to a prop attached under <paramref name="owner"/> rather than to the owner itself. </summary>
    public static bool IsAttachedPropRenderer(Renderer renderer, UmaContainer owner)
    {
        var prop = renderer.GetComponentInParent<UmaContainerProp>();
        return prop != null && prop != owner;
    }
}
