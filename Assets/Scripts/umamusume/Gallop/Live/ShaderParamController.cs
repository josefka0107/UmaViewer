using System;
using System.Collections.Generic;
using UnityEngine;

namespace Gallop.Live.ShaderParam
{
    /// <summary>
    /// The game's component on live stages that sets global shader parameters (live10151 sets _AmbientColor, "color of
    /// everything", and _CharaColor, "color of the characters", to white). Field names match the serialized data, so
    /// Unity fills them when the stage loads; without it those globals keep their default of zero.
    /// </summary>
    public class ShaderParamController : MonoBehaviour
    {
        [Serializable]
        public class ParamVector
        {
            /// <summary> Set once when the stage starts instead of every frame. </summary>
            public bool StartOnly;
            public string Name;
            public string Comment;
            public Vector4 Param;
        }

        [Serializable]
        public class ParamFloat
        {
            public bool StartOnly;
            public string Name;
            public string Comment;
            public float Param;
        }

        [Serializable]
        public class ParamInt
        {
            public bool StartOnly;
            public string Name;
            public string Comment;
            public int Param;
        }

        [SerializeField] private List<ParamVector> m_ShaderParamVector = new List<ParamVector>();
        [SerializeField] private List<ParamFloat> m_ShaderParamFloat = new List<ParamFloat>();
        [SerializeField] private List<ParamInt> m_ShaderParamInt = new List<ParamInt>();

        private void OnEnable()
        {
            Apply(true);
            Debug.Log($"[ShaderParamController] {name}: {m_ShaderParamVector.Count} vector, {m_ShaderParamFloat.Count} float, {m_ShaderParamInt.Count} int globals");
        }

        private void Update()
        {
            Apply(false);
        }

        private void Apply(bool start)
        {
            foreach (var p in m_ShaderParamVector)
                if (p != null && !string.IsNullOrEmpty(p.Name) && (start || !p.StartOnly))
                    Shader.SetGlobalVector(p.Name, p.Param);
            foreach (var p in m_ShaderParamFloat)
                if (p != null && !string.IsNullOrEmpty(p.Name) && (start || !p.StartOnly))
                    Shader.SetGlobalFloat(p.Name, p.Param);
            foreach (var p in m_ShaderParamInt)
                if (p != null && !string.IsNullOrEmpty(p.Name) && (start || !p.StartOnly))
                    Shader.SetGlobalInt(p.Name, p.Param);
        }
    }
}
