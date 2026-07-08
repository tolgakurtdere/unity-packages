using UnityEngine;

namespace TK.Audio
{
    /// <summary>
    /// Marks a <see cref="string"/> field as an audio entry key: the inspector shows a dropdown
    /// of keys collected from every <see cref="AudioCatalog"/> in the project instead of a raw
    /// text field. Runtime-only marker; the drawer lives in the editor assembly.
    /// </summary>
    public sealed class AudioKeyAttribute : PropertyAttribute
    {
    }
}
