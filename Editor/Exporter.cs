using System;
using System.Linq;
using System.IO;
using UnityEngine;
using UnityEditor;
using Ionic.Zip;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    internal class Exporter
    {
        private static readonly string[] ExcludedFileNames = new[] { "Exporter.cs", "SwayingObjectsConverter.cs" };

        private static readonly string PackageName = "VRM Converter for VRChat-" + Converter.Version;

        [MenuItem(itemName: "Assets/Export VRM Converter For VRChat", isValidateFunction: false, priority: 30)]
        private static void Export()
        {
            var packagePath = Path.Combine(Application.temporaryCachePath, Exporter.PackageName + ".unitypackage");
            AssetDatabase.ExportPackage(
                assetPathNames: AssetDatabase.GetAllAssetPaths()
                    .Where(path => path.StartsWith(Converter.RootFolderPath + "/") && !Exporter.ExcludedFileNames.Contains(Path.GetFileName(path: path)))
                    .ToArray(),
                fileName: packagePath
            );

            var zipFile = new ZipFile();
            zipFile.AddFile(fileName: packagePath, directoryPathInArchive: "");
            zipFile.Save(fileName: Path.Combine(Environment.GetFolderPath(folder: Environment.SpecialFolder.DesktopDirectory), Exporter.PackageName + ".zip"));

            File.Delete(path: packagePath);
        }

        [MenuItem(itemName: "Assets/Export VRM Converter For VRChat", isValidateFunction: true)]
        private static bool IsInProjectFolder()
        {
            var activeObject = Selection.activeObject;
            if (!activeObject) {
                return false;
            }

            return (AssetDatabase.GetAssetPath(assetObject: activeObject) + "/").StartsWith(Converter.RootFolderPath + "/");
        }
    }
}
