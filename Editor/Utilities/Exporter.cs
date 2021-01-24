using System;
using System.Linq;
using System.IO;
using System.Text.RegularExpressions;
using System.Diagnostics;
using UnityEngine;
using UnityEditor;
using UniGLTF;
using VRM;
using UniVRMExtensionsMenuItems = Esperecyan.UniVRMExtensions.MenuItems;

namespace Esperecyan.Unity.VRMConverterForVRChat.Utilities
{
    internal class Exporter
    {
        private static readonly Regex FilePathPattern = new Regex(
            @"^Assets/VRMConverterForVRChat/(?!Editor/Utilities/Exporter\.cs$)|^Assets/(?:VRM|UniGLTF|VRMShaders|Esperecyan(/UniVRMExtensions)?)/"
        );

        private static readonly string PackageName = $"{Converter.Name}-{Converter.Version} + UniVRMExtensions-{UniVRMExtensionsMenuItems.Version} + {VRMVersion.VRM_VERSION}";

        [MenuItem("Assets/Export " + Converter.Name, false, 30)]
        private static void Export()
        {
            var packagePath = Path.Combine(Application.temporaryCachePath, Exporter.PackageName + ".unitypackage");
            AssetDatabase.ExportPackage(
                AssetDatabase.GetAllAssetPaths().Where(path => Exporter.FilePathPattern.IsMatch(input: path)).ToArray(),
                packagePath
            );

            Process.Start(fileName: "PowerShell", arguments: $@"-Command ""Compress-Archive `
                -Path @('{packagePath}') `
                -DestinationPath '{Path.Combine(
                    Environment.GetFolderPath(folder: Environment.SpecialFolder.DesktopDirectory),
                    Exporter.PackageName + ".zip"
                )}'""")
                .WaitForExit();

            File.Delete(path: packagePath);
        }
    }
}
