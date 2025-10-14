using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditorInternal;
using UnityEngine;

namespace XmlDocGenerator
{
    internal static class AssemblyDocGenerator
    {
        private readonly struct AsmdefData
        {
            public readonly string Name;
            public readonly RootPath Root;

            public AsmdefData(string name, RootPath root)
            {
                Name = name;
                Root = root;
            }
        }

        private class PackageDef
        {
            public readonly RootPath Root;
            public readonly List<AsmdefData> Asmdefs = new();

            public PackageDef(RootPath root)
            {
                Root = root;
            }
        }

        private const string GENERATED_FILE = ".XMLDOC_CSC_RSP_GENERATED";

        [MenuItem("Tools/Generate XML Documentation")]
        private static void GenerateXmlDocumentation()
        {
            const string TITLE = "Generate XML Documentation";
            const string INFO = "Generating...";

            EditorUtility.DisplayProgressBar(TITLE, INFO, 0f);

            var guidStrings = AssetDatabase
                .FindAssets($"t:{nameof(AssemblyDefinitionAsset)}")
                .AsSpan();

            var guidStringsLength = guidStrings.Length;

            if (guidStringsLength < 1)
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            EditorUtility.DisplayProgressBar(TITLE, INFO, 10f);

            RootPath projectRoot = Application.dataPath.Replace("/Assets", string.Empty);

            var packageMap = new Dictionary<string, PackageDef>();

            for (var i = 0; i < guidStringsLength; i++)
            {
                var asmdefGuidString = guidStrings[i];
                var asmdefPath = AssetDatabase.GUIDToAssetPath(asmdefGuidString).Replace('\\', '/');
                var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(asmdefPath);
                var asmdefAsset = AssetDatabase.LoadAssetAtPath<AssemblyDefinitionAsset>(asmdefPath);

                if (package == null || package.source == PackageSource.Embedded || asmdefAsset == false)
                {
                    continue;
                }

                if (packageMap.TryGetValue(package.name, out var packageDef) == false)
                {
                    packageMap[package.name] = packageDef = new(package.resolvedPath);
                }

                try
                {
                    var prefixLength = 9 + package.name.Length + 1;
                    var asmdefFolderPath = asmdefPath[prefixLength..^($"{asmdefAsset.name}.asmdef".Length)];

                    if (asmdefFolderPath.EndsWith('/'))
                    {
                        asmdefFolderPath = asmdefFolderPath[..^1];
                    }

                    RootPath asmdefRoot = packageDef.Root.GetFolderAbsolutePath(asmdefFolderPath);
                    var genenratedFilePath = asmdefRoot.GetFileAbsolutePath(GENERATED_FILE);

                    if (File.Exists(genenratedFilePath) == false)
                    {
                        packageDef.Asmdefs.Add(new AsmdefData(asmdefAsset.name, asmdefRoot));
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Exception occurred while processing '{asmdefPath}': {ex.Message}");
                }
            }

            if (packageMap.Count < 1)
            {
                EditorUtility.ClearProgressBar();
                return;
            }

            EditorUtility.DisplayProgressBar(TITLE, INFO, 20f);

            var printer = new Printer(1024);

            foreach (var package in packageMap.Values)
            {
                foreach (var asmdef in package.Asmdefs)
                {
                    var cscFilePath = asmdef.Root.GetFileAbsolutePath("csc.rsp");
                    var genenratedFilePath = asmdef.Root.GetFileAbsolutePath(GENERATED_FILE);

                    printer.Clear();

                    var cscFileExists = false;

                    if (File.Exists(cscFilePath))
                    {
                        cscFileExists = true;
                        printer.PrintLine(File.ReadAllText(cscFilePath));
                        File.Delete(cscFilePath);
                    }

                    File.WriteAllText(cscFilePath, GetCscRspContent(ref printer, asmdef.Name), Encoding.UTF8);

                    if (cscFileExists == false)
                    {
                        printer.Clear();

                        File.WriteAllText(
                              asmdef.Root.GetFileAbsolutePath("csc.rsp.meta")
                            , GetCscRspMetaContent(ref printer)
                            , Encoding.UTF8
                        );
                    }

                    File.WriteAllText(genenratedFilePath, "");
                }
            }

            EditorUtility.DisplayProgressBar(TITLE, INFO, 100f);
            EditorUtility.ClearProgressBar();
        }

        private static string GetCscRspContent(ref Printer p, string asmdefName)
        {
            // https://gamedev.stackexchange.com/a/173674
            p.PrintBeginLine($"-doc:Library/ScriptAssemblies/{asmdefName}.xml ")
                .Print("-nowarn:1570 -nowarn:1591 -nowarn:1584 -nowarn:1658 -nowarn:419 ")
                .PrintEndLine("-nowarn:1574 -nowarn:1572 -nowarn:1573 -nowarn:1587");
            p.PrintEndLine();

            return p.Result;
        }

        private static string GetCscRspMetaContent(ref Printer p)
        {
            p.PrintLine("fileFormatVersion: 2");
            p.PrintBeginLine("guid: ").PrintEndLine(Guid.NewGuid().ToString("N"));
            p.PrintLine("DefaultImporter:");
            p.PrintLine("  externalObjects: {}");
            p.PrintLine("  userData: ");
            p.PrintLine("  assetBundleName: ");
            p.PrintLine("  assetBundleVariant: ");
            p.PrintEndLine();

            return p.Result;
        }
    }
}
