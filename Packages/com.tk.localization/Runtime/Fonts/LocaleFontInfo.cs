using TMPro;
using UnityEngine;

namespace TK.Localization
{
    /// <summary>One locale's TMP font bundle: font asset, optional material preset, and RTL direction flag.</summary>
    [CreateAssetMenu(fileName = "LocaleFontInfo", menuName = "TK/Localization/Locale Font Info")]
    public sealed class LocaleFontInfo : ScriptableObject
    {
        [SerializeField] private TMP_FontAsset _font;
        [SerializeField] private Material _material;   // optional; may be null
        [SerializeField] private bool _rightToLeft;

        public TMP_FontAsset Font => _font;
        public Material Material => _material;
        public bool RightToLeft => _rightToLeft;
    }
}
