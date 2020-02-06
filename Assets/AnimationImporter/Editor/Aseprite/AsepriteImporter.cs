using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using AnimationImporter.Boomlagoon.JSON;
using UnityEditor;
using System.IO;

namespace AnimationImporter.Aseprite
{
    [InitializeOnLoad]
    public class AsepriteImporter : IAnimationImporterPlugin
    {
        // ================================================================================
        //  const
        // --------------------------------------------------------------------------------

        const string ASEPRITE_STANDARD_PATH_WINDOWS = @"C:\Program Files (x86)\Aseprite\Aseprite.exe";
        const string ASEPRITE_STANDARD_PATH_MACOSX = @"/Applications/Aseprite.app/Contents/MacOS/aseprite";

        public static string standardApplicationPath
        {
            get
            {
                if (Application.platform == RuntimePlatform.WindowsEditor)
                {
                    return ASEPRITE_STANDARD_PATH_WINDOWS;
                }
                else
                {
                    return ASEPRITE_STANDARD_PATH_MACOSX;
                }
            }
        }

        // ================================================================================
        //  static constructor, registering plugin
        // --------------------------------------------------------------------------------

        static AsepriteImporter()
        {
            AsepriteImporter importer = new AsepriteImporter();
            AnimationImporter.RegisterImporter(importer, "ase", "aseprite");
        }

        // ================================================================================
        //  public methods
        // --------------------------------------------------------------------------------

        public ImportedAnimationSheet Import(AnimationImportJob job, AnimationImporterSharedConfig config)
        {
            Texture2D srcTex;
            JSONObject metaData;
            if (CreateSpriteAtlasAndMetaFile(job, out srcTex, out metaData))
            {
                ImportedAnimationSheet animationSheet = CreateAnimationSheetFromMetaData(job, config, srcTex, metaData);

                return animationSheet;
            }

            return null;
        }

        public bool IsValid()
        {
            return AnimationImporter.Instance != null && AnimationImporter.Instance.sharedData != null;
        }

        public bool IsConfigured()
        {
            return File.Exists(Path.GetFullPath(AnimationImporter.Instance.asepritePath));
        }

        // ================================================================================
        //  private methods
        // --------------------------------------------------------------------------------

        // parses a JSON file and creates the raw data for ImportedAnimationSheet from it
        private static ImportedAnimationSheet CreateAnimationSheetFromMetaData(AnimationImportJob job, AnimationImporterSharedConfig config, Texture2D srcTex, JSONObject metadata)
        {
            job.SetProgress(0.2f, "getting animation sheet...");
            if (metadata != null && srcTex != null)
            {
                ImportedAnimationSheet animationSheet = GetAnimationInfo(metadata);

                if (animationSheet == null)
                {
                    return null;
                }
                animationSheet.srcTex = srcTex;

                if (!animationSheet.hasAnimations)
                {
                    Debug.LogWarning("No Animations found in Aseprite file. Use Aseprite Tags to assign names to Animations.");
                }

                animationSheet.SetNonLoopingAnimations(config.animationNamesThatDoNotLoop);

                return animationSheet;
            }

            return null;
        }

        /// <summary>
        /// calls the Aseprite application which then should output a png with all sprites and a corresponding JSON
        /// </summary>
        /// <returns></returns>
        private static bool CreateSpriteAtlasAndMetaFile(AnimationImportJob job, out Texture2D img, out JSONObject metadata)
        {
            job.SetProgress(0, "Invoking Aseprite CLI...");
            img = null;
            metadata = null;
            char delimiter = '\"';
            string parameters = "--data " + delimiter + job.name + ".json" + delimiter + " --sheet " + delimiter + job.name + ".png" + delimiter + " --sheet-pack --list-tags --format json-array " + delimiter + job.fileName + delimiter;

            if (!string.IsNullOrEmpty(job.additionalCommandLineArguments))
            {
                parameters = job.additionalCommandLineArguments + " " + parameters;
            }

            bool success = CallAsepriteCLI(AnimationImporter.Instance.asepritePath, job.assetDirectory, parameters) == 0;

            job.SetProgress(0.1f, "Loading Back CLI results...");

            // don't do stupid shit here, just load stuff from disk then delete them!
            // // move png and json file to subfolder
            if (success && job.directoryPathForSprites != job.assetDirectory)
            {
                // create subdirectory
                if (!Directory.Exists(job.directoryPathForSprites))
                {
                    Directory.CreateDirectory(job.directoryPathForSprites);
                }

                string jsonSource = job.assetDirectory + "/" + job.name + ".json";
                if (File.Exists(jsonSource))
                {
                    using (var jsonFile = File.OpenText(jsonSource))
                    {
                        metadata = JSONObject.Parse(jsonFile.ReadToEnd());
                    }
                    File.Delete(jsonSource);
                }
                else
                {
                    Debug.LogWarning("Calling Aseprite resulted in no json data file. Wrong Aseprite version?");
                    return false;
                }

                // check and copy png file
                string pngSource = job.assetDirectory + "/" + job.name + ".png";
                if (File.Exists(pngSource))
                {
                    using (var pngFile = File.OpenRead(pngSource))
                    {
                        var buffer = new byte[pngFile.Length];
                        pngFile.Read(buffer, 0, (int)pngFile.Length);
                        img = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                        ImageConversion.LoadImage(img, buffer, false);
                        img.name = job.name;
                    }
                    File.Delete(pngSource);
                }
                else
                {
                    Debug.LogWarning("Calling Aseprite resulted in no png Image file. Wrong Aseprite version?");
                    return false;
                }
            }

            return success;
        }

        private static ImportedAnimationSheet GetAnimationInfo(JSONObject root)
        {
            if (root == null)
            {
                Debug.LogWarning("Error importing JSON animation info: JSONObject is NULL");
                return null;
            }

            ImportedAnimationSheet animationSheet = new ImportedAnimationSheet();

            // import all informations from JSON

            if (!root.ContainsKey("meta"))
            {
                Debug.LogWarning("Error importing JSON animation info: no 'meta' object");
                return null;
            }
            var meta = root["meta"].Obj;
            GetMetaInfosFromJSON(animationSheet, meta);

            if (GetAnimationsFromJSON(animationSheet, meta) == false)
            {
                return null;
            }

            if (GetFramesFromJSON(animationSheet, root) == false)
            {
                return null;
            }

            animationSheet.ApplyGlobalFramesToAnimationFrames();

            return animationSheet;
        }

        private static int CallAsepriteCLI(string asepritePath, string path, string buildOptions)
        {
            string workingDirectory = Application.dataPath.Replace("Assets", "") + path;

            System.Diagnostics.ProcessStartInfo start = new System.Diagnostics.ProcessStartInfo();
            start.Arguments = "-b " + buildOptions;
            start.FileName = Path.GetFullPath(asepritePath);
            start.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            start.CreateNoWindow = true;
            start.UseShellExecute = false;
            start.WorkingDirectory = workingDirectory;

            // Run the external process & wait for it to finish
            using (System.Diagnostics.Process proc = System.Diagnostics.Process.Start(start))
            {
                proc.WaitForExit();
                // Retrieve the app's exit code
                return proc.ExitCode;
            }
        }

        private static void GetMetaInfosFromJSON(ImportedAnimationSheet animationSheet, JSONObject meta)
        {
            var size = meta["size"].Obj;
            animationSheet.width = (int)size["w"].Number;
            animationSheet.height = (int)size["h"].Number;
        }

        private static bool GetAnimationsFromJSON(ImportedAnimationSheet animationSheet, JSONObject meta)
        {
            if (!meta.ContainsKey("frameTags"))
            {
                Debug.LogWarning("No 'frameTags' found in JSON created by Aseprite.");
                IssueVersionWarning();
                return false;
            }

            var frameTags = meta["frameTags"].Array;
            foreach (var item in frameTags)
            {
                JSONObject frameTag = item.Obj;
                ImportedAnimation anim = new ImportedAnimation();
                anim.name = frameTag["name"].Str;
                anim.firstSpriteIndex = (int)(frameTag["from"].Number);
                anim.lastSpriteIndex = (int)(frameTag["to"].Number);

                switch (frameTag["direction"].Str)
                {
                    default:
                        anim.direction = PlaybackDirection.Forward;
                        break;
                    case "reverse":
                        anim.direction = PlaybackDirection.Reverse;
                        break;
                    case "pingpong":
                        anim.direction = PlaybackDirection.PingPong;
                        break;
                }

                animationSheet.animations.Add(anim);
            }

            return true;
        }

        private static bool GetFramesFromJSON(ImportedAnimationSheet animationSheet, JSONObject root)
        {
            var list = root["frames"].Array;

            if (list == null)
            {
                Debug.LogWarning("No 'frames' array found in JSON created by Aseprite.");
                IssueVersionWarning();
                return false;
            }

            foreach (var item in list)
            {
                ImportedAnimationFrame frame = new ImportedAnimationFrame();

                var frameValues = item.Obj["frame"].Obj;
                frame.width = (int)frameValues["w"].Number;
                frame.height = (int)frameValues["h"].Number;
                frame.x = (int)frameValues["x"].Number;
                frame.y = animationSheet.height - (int)frameValues["y"].Number - frame.height; // unity has a different coord system

                frame.duration = (int)item.Obj["duration"].Number;

                animationSheet.frames.Add(frame);
            }

            return true;
        }

        private static void IssueVersionWarning()
        {
            Debug.LogWarning("Please use official Aseprite 1.1.1 or newer.");
        }
    }
}