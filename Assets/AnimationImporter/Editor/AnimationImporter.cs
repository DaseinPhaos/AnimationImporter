using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System.IO;
using UnityEditor.Animations;
using System.Linq;
using AnimationImporter.Aseprite;
using Blingame.Importers;

namespace AnimationImporter {
    public class AnimationImporter {
        // ================================================================================
        //	Singleton
        // --------------------------------------------------------------------------------

        private static AnimationImporter _instance = null;
        public static AnimationImporter Instance {
            get {
                if (_instance == null) {
                    _instance = new AnimationImporter();
                }

                return _instance;
            }
        }

        // ================================================================================
        //  delegates
        // --------------------------------------------------------------------------------

        public delegate ImportedAnimationSheet ImportDelegate(AnimationImportJob job, AnimationImporterSharedConfig config);

        public delegate bool CustomReImportDelegate(string fileName);
        public static CustomReImportDelegate HasCustomReImport = null;
        public static CustomReImportDelegate HandleCustomReImport = null;

        public delegate void ChangeImportJob(AnimationImportJob job);

        // ================================================================================
        //  const
        // --------------------------------------------------------------------------------

        private const string PREFS_PREFIX = "ANIMATION_IMPORTER_";
        private const string SHARED_CONFIG_PATH = "Assets/Resources/AnimationImporter/AnimationImporterConfig.asset";

        // ================================================================================
        //  user values
        // --------------------------------------------------------------------------------

        string _asepritePath = "";
        public string asepritePath {
            get {
                return _asepritePath;
            }
            set {
                if (_asepritePath != value) {
                    _asepritePath = value;
                    SaveUserConfig();
                }
            }
        }

        private RuntimeAnimatorController _baseController = null;
        public RuntimeAnimatorController baseController {
            get {
                return _baseController;
            }
            set {
                if (_baseController != value) {
                    _baseController = value;
                    SaveUserConfig();
                }
            }
        }

        private AnimationImporterSharedConfig _sharedData;
        public AnimationImporterSharedConfig sharedData {
            get {
                return _sharedData;
            }
        }

        // ================================================================================
        //  Importer Plugins
        // --------------------------------------------------------------------------------

        private static Dictionary<string, IAnimationImporterPlugin> _importerPlugins = new Dictionary<string, IAnimationImporterPlugin>();

        public static void RegisterImporter(IAnimationImporterPlugin importer, params string[] extensions) {
            foreach (var extension in extensions) {
                _importerPlugins[extension] = importer;
            }
        }

        // ================================================================================
        //  validation
        // --------------------------------------------------------------------------------

        // this was used in the past, might be again in the future, so leave it here
        public bool canImportAnimations {
            get {
                return true;
            }
        }
        public bool canImportAnimationsForOverrideController {
            get {
                return canImportAnimations && _baseController != null;
            }
        }

        // ================================================================================
        //  save and load user values
        // --------------------------------------------------------------------------------

        public void LoadOrCreateUserConfig() {
            LoadPreferences();

            _sharedData = ScriptableObjectUtility.LoadOrCreateSaveData<AnimationImporterSharedConfig>(SHARED_CONFIG_PATH);
        }

        public void LoadUserConfig() {
            LoadPreferences();

            _sharedData = ScriptableObjectUtility.LoadSaveData<AnimationImporterSharedConfig>(SHARED_CONFIG_PATH);
        }

        private void LoadPreferences() {
            if (PlayerPrefs.HasKey(PREFS_PREFIX + "asepritePath")) {
                _asepritePath = PlayerPrefs.GetString(PREFS_PREFIX + "asepritePath");
            } else {
                _asepritePath = AsepriteImporter.standardApplicationPath;

                if (!File.Exists(_asepritePath))
                    _asepritePath = "";
            }

            if (PlayerPrefs.HasKey(PREFS_PREFIX + "baseControllerPath")) {
                string baseControllerPath = PlayerPrefs.GetString(PREFS_PREFIX + "baseControllerPath");
                if (!string.IsNullOrEmpty(baseControllerPath)) {
                    _baseController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(baseControllerPath);
                }
            }
        }

        private void SaveUserConfig() {
            PlayerPrefs.SetString(PREFS_PREFIX + "asepritePath", _asepritePath);

            if (_baseController != null) {
                PlayerPrefs.SetString(PREFS_PREFIX + "baseControllerPath", AssetDatabase.GetAssetPath(_baseController));
            } else {
                PlayerPrefs.SetString(PREFS_PREFIX + "baseControllerPath", "");
            }
        }

        // ================================================================================
        //  import methods
        // --------------------------------------------------------------------------------

        public void ImportAssets(DefaultAsset[] assets, ImportAnimatorController importAnimatorController = ImportAnimatorController.None) {
            List<AnimationImportJob> jobs = new List<AnimationImportJob>();

            foreach (var asset in assets) {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                if (!IsValidAsset(assetPath)) {
                    continue;
                }

                AnimationImportJob job = CreateAnimationImportJob(assetPath);
                job.importAnimatorController = importAnimatorController;
                jobs.Add(job);
            }

            Import(jobs.ToArray());
        }

        /// <summary>
        /// can be used by custom import pipeline
        /// </summary>
        public ImportedAnimationSheet ImportSpritesAndAnimationSheet(
            string assetPath,
            ChangeImportJob changeImportJob = null,
            string additionalCommandLineArguments = null
        ) {
            // making sure config is valid
            if (sharedData == null) {
                LoadOrCreateUserConfig();
            }

            if (!IsValidAsset(assetPath)) {
                return null;
            }

            // create a job
            AnimationImportJob job = CreateAnimationImportJob(assetPath, additionalCommandLineArguments);
            job.createUnityAnimations = false;

            if (changeImportJob != null) {
                changeImportJob(job);
            }

            return ImportJob(job);
        }

        private void Import(AnimationImportJob[] jobs) {
            if (jobs == null || jobs.Length == 0) {
                return;
            }

            float progressPerJob = 1f / jobs.Length;

            try {
                for (int i = 0; i < jobs.Length; i++) {
                    AnimationImportJob job = jobs[i];

                    job.progressUpdated += (progress, msg) => {
                        float completeProgress = i * progressPerJob + progress * progressPerJob;
                        EditorUtility.DisplayProgressBar("Importing " + job.name, msg, completeProgress);
                    };
                    ImportJob(job);
                }
                AssetDatabase.Refresh();
            } catch (Exception error) {
                Debug.LogWarning(error.ToString());
                throw;
            }

            EditorUtility.ClearProgressBar();
        }

        private ImportedAnimationSheet ImportJob(AnimationImportJob job) {
            job.SetProgress(0);

            IAnimationImporterPlugin importer = _importerPlugins[GetExtension(job.fileName)];
            ImportedAnimationSheet animationSheet = importer.Import(job, sharedData);

            animationSheet.assetDirectory = job.assetDirectory;
            animationSheet.name = job.name;

            animationSheet.ApplySpriteNamingScheme(sharedData.spriteNamingScheme);

            job.SetProgress(0.3f, "creating sprites");
            CreateSprites(animationSheet, job);

            job.SetProgress(0.7f, "creating animations");

            if (job.createUnityAnimations) {
                CreateAnimations(animationSheet, job);

                job.SetProgress(0.9f);

                if (job.importAnimatorController == ImportAnimatorController.AnimatorController) {
                    CreateAnimatorController(animationSheet);
                } else if (job.importAnimatorController == ImportAnimatorController.AnimatorOverrideController) {
                    CreateAnimatorOverrideController(animationSheet, job.useExistingAnimatorController);
                }
            }

            return animationSheet;
        }

        // ================================================================================
        //  create animator controllers
        // --------------------------------------------------------------------------------

        private void CreateAnimatorController(ImportedAnimationSheet animations) {
            AnimatorController controller;

            string directory = sharedData.animationControllersTargetLocation.GetAndEnsureTargetDirectory(animations.assetDirectory);

            // check if controller already exists; use this to not loose any references to this in other assets
            string pathForAnimatorController = directory + "/" + animations.name + ".controller";
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(pathForAnimatorController);

            if (controller == null) {
                // create a new controller and place every animation as a state on the first layer
                controller = AnimatorController.CreateAnimatorControllerAtPath(pathForAnimatorController);
                controller.AddLayer("Default");

                foreach (var animation in animations.animations) {
                    AnimatorState state = controller.layers[0].stateMachine.AddState(animation.name);
                    state.motion = animation.animationClip;
                }
            } else {
                // look at all states on the first layer and replace clip if state has the same name
                var childStates = controller.layers[0].stateMachine.states;
                foreach (var childState in childStates) {
                    AnimationClip clip = animations.GetClip(childState.state.name);
                    if (clip != null)
                        childState.state.motion = clip;
                }
            }

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
        }

        private void CreateAnimatorOverrideController(ImportedAnimationSheet animations, bool useExistingBaseController = false) {
            AnimatorOverrideController overrideController;

            string directory = sharedData.animationControllersTargetLocation.GetAndEnsureTargetDirectory(animations.assetDirectory);

            // check if override controller already exists; use this to not loose any references to this in other assets
            string pathForOverrideController = directory + "/" + animations.name + ".overrideController";
            overrideController = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(pathForOverrideController);

            RuntimeAnimatorController baseController = _baseController;
            if (useExistingBaseController && overrideController.runtimeAnimatorController != null) {
                baseController = overrideController.runtimeAnimatorController;
            }

            if (baseController != null) {
                if (overrideController == null) {
                    overrideController = new AnimatorOverrideController();
                    AssetDatabase.CreateAsset(overrideController, pathForOverrideController);
                }

                overrideController.runtimeAnimatorController = baseController;

                // set override clips
#if UNITY_5_6_OR_NEWER
                var clipPairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(overrideController.overridesCount);
                overrideController.GetOverrides(clipPairs);

                foreach (var pair in clipPairs) {
                    string animationName = pair.Key.name;
                    AnimationClip clip = animations.GetClipOrSimilar(animationName);
                    overrideController[animationName] = clip;
                }
#else
				var clipPairs = overrideController.clips;
				for (int i = 0; i < clipPairs.Length; i++)
				{
					string animationName = clipPairs[i].originalClip.name;
					AnimationClip clip = animations.GetClipOrSimilar(animationName);
					clipPairs[i].overrideClip = clip;
				}
				overrideController.clips = clipPairs;
#endif

                EditorUtility.SetDirty(overrideController);
            } else {
                Debug.LogWarning("No Animator Controller found as a base for the Override Controller");
            }
        }

        // ================================================================================
        //  create sprites and animations
        // --------------------------------------------------------------------------------

        private void CreateAnimations(ImportedAnimationSheet animationSheet, AnimationImportJob job) {
            if (animationSheet == null) {
                return;
            }

            if (animationSheet.hasAnimations) {
                string targetPath = _sharedData.animationsTargetLocation.GetAndEnsureTargetDirectory(animationSheet.assetDirectory);
                CreateAnimationAssets(animationSheet, job.imageAssetFilename, targetPath);
            }
        }

        private void CreateAnimationAssets(ImportedAnimationSheet animationInfo, string imageAssetFilename, string pathForAnimations) {
            string masterName = Path.GetFileNameWithoutExtension(imageAssetFilename);

            foreach (var animation in animationInfo.animations) {
                animationInfo.CreateAnimation(animation, pathForAnimations, masterName, sharedData.targetObjectType, sharedData.clipFramerate, sharedData.forceToClipFramerate, sharedData.animationBindingPrefix);
            }
        }

        public static void SaveAsPng(Texture2D tex, string relativePath) {
            var pngBytes = ImageConversion.EncodeToPNG(tex);
            if (relativePath.StartsWith("Assets")) {
                relativePath = relativePath.Substring("Assets".Length);
            }
            var path = string.Format("{0}/{1}", Application.dataPath, relativePath);
            var dir = System.IO.Path.GetDirectoryName(path);
            if (!System.IO.Directory.Exists(dir)) {
                System.IO.Directory.CreateDirectory(dir);
            }
            using (var fs = System.IO.File.Create(path)) {
                fs.Write(pngBytes, 0, pngBytes.Length);
            }
        }

        public static void ClearTexture(Texture2D tex, Color color) {
            for (int y = 0; y < tex.height; ++y) {
                for (int x = 0; x < tex.width; ++x) {
                    tex.SetPixel(x, y, color);
                }
            }
            tex.Apply();
        }

        private void CreateSprites(ImportedAnimationSheet animationSheet, AnimationImportJob job) {
            if (animationSheet == null) {
                return;
            }
            var spriteInfos = new List<NamedSpriteInfo>();
            animationSheet.CreateSpriteInfos(
                in sharedData.customPivotSettings,
                spriteInfos
            );

            var siMap = new Dictionary<uint, NamedSpriteInfo>();
            var nameMap = new Dictionary<string, string>();

            var outputTexs = new Dictionary<Texture2D, SpriteMetaData[]>();

            if (sharedData.doTrim) {
                var srcTex = animationSheet.srcTex;
                if (srcTex == null) {
                    return; //?
                }

                var trimIndexList = new List<uint>();
                for (int i = 0; i < spriteInfos.Count; ++i) {
                    job.SetProgress(0.4f + i / (10.0f * spriteInfos.Count), "trimming sprite " + i.ToString());
                    var trimmed = spriteInfos[i].info.Trim(sharedData.trimColor, sharedData.trimMargin.x, sharedData.trimMargin.y);
                    var trimmedCrc = trimmed.GetCrc32();
                    if (!siMap.ContainsKey(trimmedCrc)) {
                        siMap[trimmedCrc] = new NamedSpriteInfo {
                            name = spriteInfos[i].name,
                            info = trimmed,
                        };
                        trimIndexList.Add(trimmedCrc);
                    }
                    nameMap[spriteInfos[i].name] = siMap[trimmedCrc].name;
                }

                trimIndexList.Sort((lhsi, rhsi) => {
                    var lhs = siMap[lhsi].info.frame;
                    var rhs = siMap[rhsi].info.frame;
                    if (lhs.width == rhs.width) {
                        return lhs.height - rhs.height;
                    }
                    return rhs.width - lhs.width;

                    // return (rhs.width + rhs.height) - (lhs.width + lhs.height);
                });

                var boxs = new Luxko.Geometry.SkylinePacker.Box[trimIndexList.Count];
                for (int i = 0; i < trimIndexList.Count; ++i) {
                    var spriteInfo = siMap[trimIndexList[i]].info;
                    boxs[i].w = spriteInfo.frame.width + sharedData.trimSpacing.x;
                    boxs[i].h = spriteInfo.frame.height + sharedData.trimSpacing.y;
                }

                job.SetProgress(0.5f, "caculate packing info");
                var packOutput = new Luxko.Geometry.SkylinePacker.Output[trimIndexList.Count];
                var packBin = new Luxko.Geometry.SkylinePacker.Box {
                    w = sharedData.trimTexSize.x, h = sharedData.trimTexSize.y
                };

                var sky = new Luxko.Geometry.SkylinePacker.Sky(packBin, sharedData.PackSpreadFactor > 0 ? sharedData.PackSpreadFactor : (int)(packBin.h * 0.75f), boxs);
                for (int i = 0; i < boxs.Length; ++i) {
                    sky.PackNext(out packOutput[i]);
                }

                var sheetBuf = new List<KeyValuePair<Texture2D, List<SpriteMetaData>>>();
                for (int i = 0; i < packOutput.Length; ++i) {
                    while (sheetBuf.Count <= packOutput[i].binIndex) {
                        var newTex = new Texture2D(sharedData.trimTexSize.x, sharedData.trimTexSize.y, TextureFormat.RGBA32, false); // TODO: proper linear flag
                        ClearTexture(newTex, Color.clear);
                        sheetBuf.Add(new KeyValuePair<Texture2D, List<SpriteMetaData>>(
                            newTex, new List<SpriteMetaData>()
                        ));
                    }

                    var tSheet = sheetBuf[packOutput[i].binIndex];
                    var ti = new SpritePacker.SpriteInfo {
                        tex = tSheet.Key,
                        frame = new RectInt(packOutput[i].pos.x, packOutput[i].pos.y, 0, 0)
                    };
                    var tis = siMap[trimIndexList[packOutput[i].boxIndex]];
                    job.SetProgress(0.55f + i / (20.0f * trimIndexList.Count), string.Format("packing {0} at {1} to {2}", tis.name, tis.info.frame, ti.frame.position));
                    tis.info.TryCopyTo(ref ti);
                    tSheet.Value.Add(new SpriteMetaData {
                        name = tis.name,
                        rect = new Rect(ti.frame.x, ti.frame.y, ti.frame.width, ti.frame.height),
                        alignment = (int)SpriteAlignment.Custom,
                        pivot = ti.pivotN,
                    });
                }
                foreach (var kv in sheetBuf) {
                    outputTexs.Add(kv.Key, kv.Value.ToArray());
                }

                Texture2D.DestroyImmediate(srcTex, true);
            } else {
                var targetTex = animationSheet.srcTex;
                if (spriteInfos.Count > 0) {
                    var smds = new SpriteMetaData[spriteInfos.Count];
                    for (int si = 0; si < spriteInfos.Count; ++si) {
                        smds[si] = new SpriteMetaData {
                            name = spriteInfos[si].name,
                            rect = new Rect(spriteInfos[si].info.frame.x, spriteInfos[si].info.frame.y, spriteInfos[si].info.frame.width, spriteInfos[si].info.frame.height),
                            alignment = (int)SpriteAlignment.Custom,
                            pivot = spriteInfos[si].info.pivotN,
                        };
                    }

                    outputTexs.Add(targetTex, smds);
                    spriteInfos.Clear();
                }
            }

            int kvi = 0;
            var spriteDict = new Dictionary<string, Sprite>();
            foreach (var kv in outputTexs) {
                job.SetProgress(0.6f + 0.1f * (kvi / (float)outputTexs.Count), "saving textures " + kvi);
                string imgPath;
                if (kvi <= 0) {
                    imgPath = string.Format("{0}/{1}.png", job.directoryPathForSprites, job.name);
                } else {
                    imgPath = string.Format("{0}/{1}_{2}.png", job.directoryPathForSprites, job.name, kvi);
                }
                kvi++;

                SaveAsPng(kv.Key, imgPath);

                AssetDatabase.Refresh();
                AssetDatabase.ImportAsset(imgPath, ImportAssetOptions.ForceUpdate);

                var importer = AssetImporter.GetAtPath(imgPath) as TextureImporter;
                importer.textureType = TextureImporterType.Sprite;
                importer.spritePixelsPerUnit = sharedData.spritePixelsPerUnit;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                // TODO: smart update: how to keep old references alive?
                importer.spritesheet = kv.Value;
                importer.spriteImportMode = SpriteImportMode.Multiple;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.CompressedHQ;

                EditorUtility.SetDirty(importer);

                importer.SaveAndReimport();

                AssetDatabase.ImportAsset(imgPath, ImportAssetOptions.ForceUpdate);

                var assets = AssetDatabase.LoadAllAssetsAtPath(imgPath);

                foreach (var item in assets) {
                    if (item is Sprite) {
                        var sprite = (Sprite)item;
                        spriteDict[sprite.name] = sprite;
                    }
                }
            }

            if (nameMap.Count != 0) {
                var replaceMap = new Dictionary<string, Sprite>();
                foreach (var kv in nameMap) {
                    replaceMap[kv.Key] = spriteDict[kv.Value];
                }
                spriteDict = replaceMap;
            }
            foreach (var kv in outputTexs) {
                Texture2D.DestroyImmediate(kv.Key);
            }
            outputTexs.Clear();
            animationSheet.ApplyCreatedSprites(spriteDict);
        }

        // ================================================================================
        //  querying existing assets
        // --------------------------------------------------------------------------------

        // check if this is a valid file; we are only looking at the file extension here
        public static bool IsValidAsset(string path) {
            string extension = GetExtension(path);

            if (!string.IsNullOrEmpty(path)) {
                if (_importerPlugins.ContainsKey(extension)) {
                    IAnimationImporterPlugin importer = _importerPlugins[extension];
                    if (importer != null) {
                        return importer.IsValid();
                    }
                }
            }

            return false;
        }

        // check if there is a configured importer for the specified extension
        public static bool IsConfiguredForAssets(DefaultAsset[] assets) {
            foreach (var asset in assets) {
                string assetPath = AssetDatabase.GetAssetPath(asset);
                string extension = GetExtension(assetPath);

                if (!string.IsNullOrEmpty(assetPath)) {
                    if (_importerPlugins.ContainsKey(extension)) {
                        IAnimationImporterPlugin importer = _importerPlugins[extension];
                        if (importer != null) {
                            if (!importer.IsConfigured()) {
                                return false;
                            }
                        }
                    }
                }
            }

            return true;
        }

        private static string GetExtension(string path) {
            if (string.IsNullOrEmpty(path)) {
                return null;
            }

            string extension = Path.GetExtension(path);
            if (extension.StartsWith(".")) {
                extension = extension.Remove(0, 1);
            }

            return extension;
        }

        public bool HasExistingRuntimeAnimatorController(string assetPath) {
            return HasExistingAnimatorController(assetPath) || HasExistingAnimatorOverrideController(assetPath);
        }

        public bool HasExistingAnimatorController(string assetPath) {
            return GetExistingAnimatorController(assetPath) != null;
        }

        public bool HasExistingAnimatorOverrideController(string assetPath) {
            return GetExistingAnimatorOverrideController(assetPath) != null;
        }

        public RuntimeAnimatorController GetExistingRuntimeAnimatorController(string assetPath) {
            AnimatorController animatorController = GetExistingAnimatorController(assetPath);
            if (animatorController != null) {
                return animatorController;
            }

            return GetExistingAnimatorOverrideController(assetPath);
        }

        public AnimatorController GetExistingAnimatorController(string assetPath) {
            string name = Path.GetFileNameWithoutExtension(assetPath);
            string basePath = GetBasePath(assetPath);
            string targetDirectory = sharedData.animationControllersTargetLocation.GetTargetDirectory(basePath);

            string pathForController = targetDirectory + "/" + name + ".controller";
            AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(pathForController);

            return controller;
        }

        public AnimatorOverrideController GetExistingAnimatorOverrideController(string assetPath) {
            string name = Path.GetFileNameWithoutExtension(assetPath);
            string basePath = GetBasePath(assetPath);
            string targetDirectory = sharedData.animationControllersTargetLocation.GetTargetDirectory(basePath);

            string pathForController = targetDirectory + "/" + name + ".overrideController";
            AnimatorOverrideController controller = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(pathForController);

            return controller;
        }

        // ================================================================================
        //  automatic ReImport
        // --------------------------------------------------------------------------------

        /// <summary>
        /// will be called by the AssetPostProcessor
        /// </summary>
        public void AutomaticReImport(string[] assetPaths) {
            if (sharedData == null) {
                LoadOrCreateUserConfig();
            }

            List<AnimationImportJob> jobs = new List<AnimationImportJob>();

            foreach (var assetPath in assetPaths) {
                if (string.IsNullOrEmpty(assetPath)) {
                    continue;
                }

                if (HandleCustomReImport != null && HandleCustomReImport(assetPath)) {
                    continue;
                }

                AnimationImportJob job = CreateAnimationImportJob(assetPath);
                if (job != null) {
                    if (HasExistingAnimatorController(assetPath)) {
                        job.importAnimatorController = ImportAnimatorController.AnimatorController;
                    } else if (HasExistingAnimatorOverrideController(assetPath)) {
                        job.importAnimatorController = ImportAnimatorController.AnimatorOverrideController;
                        job.useExistingAnimatorController = true;
                    }

                    jobs.Add(job);
                }
            }

            Import(jobs.ToArray());
        }

        // ================================================================================
        //  private methods
        // --------------------------------------------------------------------------------

        private AnimationImportJob CreateAnimationImportJob(string assetPath, string additionalCommandLineArguments = "") {
            AnimationImportJob importJob = new AnimationImportJob(assetPath);

            importJob.additionalCommandLineArguments = additionalCommandLineArguments;

            importJob.directoryPathForSprites = _sharedData.spritesTargetLocation.GetTargetDirectory(importJob.assetDirectory);
            importJob.directoryPathForAnimations = _sharedData.animationsTargetLocation.GetTargetDirectory(importJob.assetDirectory);
            importJob.directoryPathForAnimationControllers = _sharedData.animationControllersTargetLocation.GetTargetDirectory(importJob.assetDirectory);

            return importJob;
        }

        private string GetBasePath(string path) {
            string extension = Path.GetExtension(path);
            if (extension.Length > 0 && extension[0] == '.') {
                extension = extension.Remove(0, 1);
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            string lastPart = "/" + fileName + "." + extension;

            return path.Replace(lastPart, "");
        }
    }
}
