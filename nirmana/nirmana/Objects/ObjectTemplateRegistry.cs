using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace nirmana.Objects
{
    /// <summary>
    /// Daftar SEMUA IObjectTemplate yang muncul di menu Add — di-scan
    /// OTOMATIS pakai reflection, BUKAN didaftar manual satu-satu.
    ///
    /// CARA NAMBAH PRIMITIF BARU (beneran cuma ini, tidak ada langkah lain):
    ///   1. Bikin file baru di folder Objects/ (misal "Objects/ConeTemplate.cs"),
    ///      class-nya implement IObjectTemplate (contoh: lihat CubeTemplate.cs
    ///      untuk yang tanpa dialog, atau RoundedBoxTemplate.cs untuk yang
    ///      butuh dialog parameter).
    ///   2. Kalau perlu generator geometri baru, taruh di
    ///      Rendering/MeshEditing/ (contoh: lihat RoundedBoxGenerator.cs).
    ///   3. SELESAI. Rebuild — otomatis muncul di menu Add. Tidak perlu
    ///      edit file ini atau file manapun yang lain.
    ///
    /// CATATAN: Armature sengaja TIDAK termasuk di sini, karena alur
    /// pembuatannya beda total (bikin Skeleton, bukan Mesh/EditableMesh) —
    /// itu tetap jadi menu item terpisah di MainForm.UI.cs (AddArmature()).
    /// </summary>
    internal static class ObjectTemplateRegistry
    {
        private static IObjectTemplate[] _cached;

        /// <summary>
        /// Semua template yang ditemukan, urut alfabetis berdasarkan nama
        /// class-nya (supaya urutan menu konsisten & bisa diprediksi tanpa
        /// perlu didaftar manual). Hasil scan di-cache setelah panggilan
        /// pertama (reflection agak "berat", tidak perlu diulang tiap kali
        /// menu Add dibuka).
        /// </summary>
        public static IObjectTemplate[] All => _cached ?? (_cached = DiscoverTemplates());

        private static IObjectTemplate[] DiscoverTemplates()
        {
            Type interfaceType = typeof(IObjectTemplate);

            IEnumerable<Type> candidateTypes = interfaceType.Assembly.GetTypes()
                .Where(t => interfaceType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .OrderBy(t => t.Name);

            List<IObjectTemplate> instances = new List<IObjectTemplate>();

            foreach (Type type in candidateTypes)
            {
                try
                {
                    // Butuh constructor tanpa parameter (semua template
                    // contoh di project ini sudah begitu — CubeTemplate,
                    // RoundedBoxTemplate, SphereTemplate).
                    if (Activator.CreateInstance(type) is IObjectTemplate instance)
                    {
                        instances.Add(instance);
                    }
                }
                catch
                {
                    // Kalau ada class yang gagal dibuat otomatis (misal
                    // constructor butuh parameter), lewati saja daripada
                    // bikin seluruh menu Add gagal dibangun.
                }
            }

            return instances.ToArray();
        }
    }
}