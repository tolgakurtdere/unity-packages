using System;
using System.Collections.Generic;
using UnityEditor;

namespace TK.Audio.Editor
{
    /// <summary>
    /// Collects audio keys from catalogs for the <c>[AudioKey]</c>/<c>[AudioPlaylistKey]</c>
    /// dropdowns. <see cref="CollectKeys"/>/<see cref="CollectPlaylistKeys"/> are pure (given
    /// catalogs) and unit-tested; the <c>Scan*</c> helpers add the project-wide asset scan.
    /// </summary>
    public static class AudioCatalogKeyIndex
    {
        public static string[] CollectKeys(IEnumerable<AudioCatalog> catalogs)
        {
            return Collect(catalogs, catalog => catalog.EntryKeys());
        }

        public static string[] CollectPlaylistKeys(IEnumerable<AudioCatalog> catalogs)
        {
            return Collect(catalogs, catalog => catalog.PlaylistKeys());
        }

        /// <summary>Every entry key across all AudioCatalog assets in the project (sorted, deduped).</summary>
        public static string[] ScanEntryKeys() => CollectKeys(LoadAllCatalogs());

        /// <summary>Every playlist key across all AudioCatalog assets in the project (sorted, deduped).</summary>
        public static string[] ScanPlaylistKeys() => CollectPlaylistKeys(LoadAllCatalogs());

        private static string[] Collect(IEnumerable<AudioCatalog> catalogs, Func<AudioCatalog, IEnumerable<string>> selector)
        {
            var set = new SortedSet<string>(StringComparer.Ordinal);
            if (catalogs != null)
            {
                foreach (var catalog in catalogs)
                {
                    if (!catalog) continue;
                    foreach (var key in selector(catalog))
                    {
                        if (!string.IsNullOrEmpty(key)) set.Add(key);
                    }
                }
            }

            var result = new string[set.Count];
            set.CopyTo(result);
            return result;
        }

        private static IEnumerable<AudioCatalog> LoadAllCatalogs()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:" + nameof(AudioCatalog)))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var catalog = AssetDatabase.LoadAssetAtPath<AudioCatalog>(path);
                if (catalog) yield return catalog;
            }
        }
    }
}
