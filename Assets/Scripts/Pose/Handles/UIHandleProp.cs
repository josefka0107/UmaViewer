using UnityEngine;

/// <summary> Root handle of a prop: moves the whole prop and can attach it to the character's hands. </summary>
public class UIHandleProp : UIHandle
{
    public static UIHandleProp CreateAsChild(UmaContainerProp owner)
    {
        var handle = new GameObject(owner.name + "_Handle").AddComponent<UIHandleProp>();
        handle.transform.parent = owner.Position;
        handle.transform.localPosition = Vector3.zero;
        handle.transform.localScale = Vector3.one;
        handle.Target = owner.Position;
        handle.Init(owner.Position.gameObject).SetColor(Color.magenta).SetScale(0.8f);

        handle.Popup.AddButton("Reset All", handle.TransformResetAll);
        handle.Popup.AddButton("Attach to Right Hand", () => owner.AttachToHand("R"));
        handle.Popup.AddButton("Attach to Left Hand", () => owner.AttachToHand("L"));
        handle.Popup.AddButton("Detach", owner.Detach);
        handle.Popup.AddButton("Link to Animation", owner.LinkToCurrentAnimation);
        handle.Popup.AddButton("Unlink Animation", owner.UnlinkCurrentAnimation);
        return handle;
    }
}
