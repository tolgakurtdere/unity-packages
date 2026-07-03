using UnityEngine;

namespace TK.Core.Utilities
{
    /// <summary>
    /// Applies per-renderer color overrides without instantiating or mutating materials.
    /// </summary>
    public static class RendererColorUtility
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>
        /// Applies a color override to all material slots on a renderer.
        /// </summary>
        public static void Apply(Renderer renderer, Color color, MaterialPropertyBlock propertyBlock)
        {
            if (!renderer)
            {
                return;
            }

            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// Applies a color override to one material slot on a renderer.
        /// </summary>
        public static void Apply(Renderer renderer, int materialIndex, Color color, MaterialPropertyBlock propertyBlock)
        {
            if (!renderer || materialIndex < 0 || materialIndex >= renderer.sharedMaterials.Length)
            {
                return;
            }

            propertyBlock.Clear();
            propertyBlock.SetColor(BaseColorId, color);
            propertyBlock.SetColor(ColorId, color);
            renderer.SetPropertyBlock(propertyBlock, materialIndex);
        }
    }
}
