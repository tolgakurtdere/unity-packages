using UnityEngine;

namespace TK.Audio
{
    /// <summary>
    /// Marks a <see cref="string"/> field as a playlist key: the inspector shows a dropdown of
    /// playlist keys collected from every <see cref="AudioCatalog"/> in the project.
    /// </summary>
    public sealed class AudioPlaylistKeyAttribute : PropertyAttribute
    {
    }
}
