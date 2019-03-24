using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using VRM;
using Ionic.Zip;

namespace Esperecyan.Unity.VRMConverterForVRChat
{
    internal class Exporter
    {
        private static readonly Regex ExcludedFilePathPattern
            = new Regex(pattern: @"/(?:Exporter\.cs|SwayingObjectsConverter\.cs|Ionic\.Zip\.dll|MToon-.+\.shader)$");

        private static readonly string PackageName = Converter.Name + "-" + Converter.Version;

        [MenuItem(itemName: "Assets/Export " + Converter.Name, isValidateFunction: false, priority: 30)]
        private static void Export()
        {
            string[] allAssetPathNames = AssetDatabase.GetAllAssetPaths();

            IEnumerable<string> assetPathNames = allAssetPathNames
                .Where(path => path.StartsWith(Converter.RootFolderPath + "/") && !Exporter.ExcludedFilePathPattern.IsMatch(input: path));
            
            IEnumerable<string> packagePaths = new[] { false, true }.Select(withUniVRM => {
                string name = Exporter.PackageName;

                if (withUniVRM) {
                    name += " + " + VRMVersion.VRM_VERSION;
                    assetPathNames = assetPathNames.Concat(allAssetPathNames.Where(path => path.StartsWith("Assets/VRM/")));
                }

                var packagePath = Path.Combine(Application.temporaryCachePath, name + ".unitypackage");
                AssetDatabase.ExportPackage(assetPathNames: assetPathNames.ToArray(), fileName: packagePath);
                return packagePath;
            });
            
            var zipFile = new ZipFile();
            zipFile.AddFiles(fileNames: packagePaths, directoryPathInArchive: "");
            zipFile.Save(fileName: Path.Combine(Environment.GetFolderPath(folder: Environment.SpecialFolder.DesktopDirectory), Exporter.PackageName + ".zip"));

            foreach (string packagePath in packagePaths) {
                File.Delete(path: packagePath);
            }
        }
    }
}
