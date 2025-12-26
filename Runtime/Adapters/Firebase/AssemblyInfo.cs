using UnityEngine.Scripting;

// Force the Unity linker to process this assembly even if no types are directly referenced in scenes.
// Required because this assembly uses [RuntimeInitializeOnLoadMethod] for auto-registration.
// See: https://docs.unity3d.com/ScriptReference/Scripting.AlwaysLinkAssemblyAttribute.html
[assembly: AlwaysLinkAssembly]
