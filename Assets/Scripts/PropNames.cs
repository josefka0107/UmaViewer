using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

/// <summary>
/// Display names for props. The game data has none, so props named after a character id
/// (prop1001_* = 1001 Special Week) get that character, and the user can name the rest.
/// User names are saved next to the application.
/// </summary>
public static class PropNames
{
    static string FilePath => $"{Application.dataPath}/../PropNames.json";

    static readonly Regex PropIdRegex = new Regex(@"prop(\d{4})_");

    static Dictionary<string, string> _names;

    static Dictionary<string, string> Names
    {
        get
        {
            if (_names == null)
            {
                _names = new Dictionary<string, string>();
                try
                {
                    if (File.Exists(FilePath))
                    {
                        _names = JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(FilePath)) ?? _names;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                    Debug.LogError($"{FilePath} is not a valid prop name file.");
                }
            }
            return _names;
        }
    }

    /// <summary> Sets the user name of a prop (AbList key); an empty name removes it. </summary>
    public static void Set(string prop, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Names.Remove(prop);
        }
        else
        {
            Names[prop] = name.Trim();
        }
        File.WriteAllText(FilePath, JsonConvert.SerializeObject(Names, Formatting.Indented));
    }

    public static string GetUserName(string prop)
    {
        return Names.TryGetValue(prop, out var name) ? name : null;
    }

    /// <summary> Label for the prop list: user name or character name, followed by the file name. </summary>
    public static string GetDisplayName(string prop)
    {
        string fileName = Path.GetFileName(prop);
        string name = GetUserName(prop);

        if (name == null)
        {
            var match = PropIdRegex.Match(fileName);
            if (match.Success)
            {
                var chara = UmaViewerMain.Instance.Characters.FirstOrDefault(c => c.Id.ToString() == match.Groups[1].Value);
                name = chara?.GetName();
            }
        }

        return name == null ? fileName : $"{name} · {fileName}";
    }
}
