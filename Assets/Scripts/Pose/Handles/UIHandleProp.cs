using System;
using UnityEngine;

/// <summary> Root handle of a prop: moves the whole prop and can attach it to the character's hands. </summary>
public class UIHandleProp : UIHandle
{
    public static UIHandleProp CreateAsChild(UmaContainer owner, Action attachRight, Action attachLeft, Action detach)
    {
        var handle = new GameObject(owner.name + "_Handle").AddComponent<UIHandleProp>();
        handle.transform.parent = owner.Position;
        handle.transform.localPosition = Vector3.zero;
        handle.transform.localScale = Vector3.one;
        handle.Target = owner.Position;
        handle.Init(owner.Position.gameObject).SetColor(Color.magenta).SetScale(0.8f);

        handle.Popup.AddButton("Reset All", handle.TransformResetAll);
        handle.Popup.AddButton("Attach to Right Hand", attachRight);
        handle.Popup.AddButton("Attach to Left Hand", attachLeft);
        handle.Popup.AddButton("Detach", detach);
        return handle;
    }
}
