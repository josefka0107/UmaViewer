using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// User-made links between a body animation and a prop held in one hand. The game data has no such
/// mapping, so links are saved next to the application and re-applied whenever the animation is loaded.
/// </summary>
public static class PropAnimationLinks
{
    public class Link
    {
        /// <summary> AbList key of the prop prefab </summary>
        public string Prop;
        /// <summary> "R" or "L" </summary>
        public string Hand;
    }

    static string FilePath => $"{Application.dataPath}/../PropAnimationLinks.json";

    static Dictionary<string, Link> _links;

    static Dictionary<string, Link> Links
    {
        get
        {
            if (_links == null)
            {
                _links = new Dictionary<string, Link>();
                try
                {
                    if (File.Exists(FilePath))
                    {
                        _links = JsonConvert.DeserializeObject<Dictionary<string, Link>>(File.ReadAllText(FilePath)) ?? _links;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Debug.LogError($"{FilePath} is not a valid prop link file.");
                }
            }
            return _links;
        }
    }

    public static void Set(string animation, string prop, string hand)
    {
        Links[animation] = new Link { Prop = prop, Hand = hand };
        Save();
    }

    public static bool Remove(string animation)
    {
        bool removed = Links.Remove(animation);
        if (removed)
        {
            Save();
        }
        return removed;
    }

    public static bool TryGet(string animation, out Link link)
    {
        link = null;
        return !string.IsNullOrEmpty(animation) && Links.TryGetValue(animation, out link);
    }

    static void Save()
    {
        File.WriteAllText(FilePath, JsonConvert.SerializeObject(Links, Formatting.Indented));
        Debug.Log($"[PropAnimationLinks] Saved {Links.Count} link(s) to {Path.GetFullPath(FilePath)}");
    }

    /// <summary> Loads and attaches the prop linked to the character's animation, and removes a previously auto-attached one. </summary>
    public static void Apply(UmaContainerCharacter character, string animation)
    {
        var builder = UmaViewerBuilder.Instance;
        var current = builder.CurrentOtherContainer as UmaContainerProp;

        if (!TryGet(animation, out var link) || !UmaViewerMain.Instance.AbList.TryGetValue(link.Prop, out var entry))
        {
            if (current != null && current.IsAutoLinked)
            {
                builder.UnloadProp();
            }
            return;
        }

        if (current != null && current.Entry == entry && current.AttachedHand == link.Hand)
        {
            return;
        }

        Debug.Log($"[PropAnimationLinks] {animation}: loading {link.Prop} in hand {link.Hand}");
        builder.LoadProp(entry);
        if (builder.CurrentOtherContainer is UmaContainerProp prop)
        {
            prop.AttachToHand(link.Hand);
            prop.IsAutoLinked = true;
        }
    }
}
