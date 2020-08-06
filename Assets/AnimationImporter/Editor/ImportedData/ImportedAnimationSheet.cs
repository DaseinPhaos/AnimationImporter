using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using System.Linq;

namespace AnimationImporter {
    public struct NamedSpriteInfo {
        public string name;
        public Blingame.Importers.SpritePacker.SpriteInfo info;
    }

    public class ImportedAnimationSheet {
        public Texture2D srcTex;
        public string name { get; set; }
        public string assetDirectory { get; set; }

        public int width { get; set; }
        public int height { get; set; }
        public int maxTextureSize {
            get {
                return Mathf.Max(width, height);
            }
        }

        public List<ImportedAnimationFrame> frames = new List<ImportedAnimationFrame>();
        public List<ImportedAnimation> animations = new List<ImportedAnimation>();

        public bool hasAnimations {
            get {
                return animations != null && animations.Count > 0;
            }
        }

        private Dictionary<string, ImportedAnimation> _animationDatabase = null;

        // ================================================================================
        //  public methods
        // --------------------------------------------------------------------------------

        // get animation by name; used when updating an existing AnimatorController 
        public AnimationClip GetClip(string clipName) {
            if (_animationDatabase == null)
                BuildIndex();

            if (_animationDatabase.ContainsKey(clipName))
                return _animationDatabase[clipName].animationClip;

            return null;
        }

        /* 
			get animation by name; used when creating an AnimatorOverrideController
			we look for similar names so the OverrideController is still functional in cases where more specific or alternative animations are not present
			idle <- idle
			idleAlt <- idle
		*/
        public AnimationClip GetClipOrSimilar(string clipName) {
            AnimationClip clip = GetClip(clipName);

            if (clip != null)
                return clip;

            List<ImportedAnimation> similarAnimations = new List<ImportedAnimation>();
            foreach (var item in animations) {
                if (clipName.Contains(item.name))
                    similarAnimations.Add(item);
            }

            if (similarAnimations.Count > 0) {
                ImportedAnimation similar = similarAnimations.OrderBy(x => x.name.Length).Reverse().First();
                return similar.animationClip;
            }

            return null;
        }

        public void CreateAnimation(ImportedAnimation anim, string basePath, string masterName, AnimationTargetObjectType targetType, int framerate, bool forceToFramerate, string bindingPath) {
            AnimationClip clip;
            string fileName = basePath + "/" + masterName + "_" + anim.name + ".anim";
            bool isLooping = anim.isLooping;

            // check if animation file already exists
            clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(fileName);
            if (clip != null) {
                // get previous animation settings
                targetType = PreviousImportSettings.GetAnimationTargetFromExistingClip(clip);
            } else {
                clip = new AnimationClip();
                AssetDatabase.CreateAsset(clip, fileName);
            }

            // framerates
            clip.frameRate = framerate;
            // change loop settings
            if (isLooping) {
                clip.wrapMode = WrapMode.Loop;
                clip.SetLoop(true);
            } else {
                clip.wrapMode = WrapMode.Clamp;
                clip.SetLoop(false);
            }

            // convert keyframes
            ImportedAnimationFrame[] srcKeyframes = anim.ListFramesAccountingForPlaybackDirection().ToArray();
            ObjectReferenceKeyframe[] keyFrames = new ObjectReferenceKeyframe[srcKeyframes.Length + 1];
            float timeOffset = 0f;

            var srEcb = AnimationClipUtility.GetSpriteRendererCurveBinding(bindingPath);
            var imgEcb = AnimationClipUtility.GetImageCurveBinding(bindingPath);

            for (int i = 0; i < srcKeyframes.Length; i++) {
                // first sprite will be set at the beginning (t=0) of the animation
                keyFrames[i] = new ObjectReferenceKeyframe {
                    time = timeOffset,
                    value = srcKeyframes[i].sprite
                };

                // add duration of frame in seconds
                timeOffset += forceToFramerate ? (1.0f / framerate) : (srcKeyframes[i].duration / 1000f);
            }

            // repeating the last frame at a point "just before the end" so the animation gets its correct length
            keyFrames[srcKeyframes.Length] = new ObjectReferenceKeyframe {
                time = timeOffset - (1f / clip.frameRate), // substract the duration of one frame
                value = srcKeyframes.Last().sprite
            };

            // save curve into clip, either for SpriteRenderer, Image, or both
            if (targetType == AnimationTargetObjectType.SpriteRenderer) {
                AnimationUtility.SetObjectReferenceCurve(clip, srEcb, keyFrames);
                AnimationUtility.SetObjectReferenceCurve(clip, imgEcb, null);
            } else if (targetType == AnimationTargetObjectType.Image) {
                AnimationUtility.SetObjectReferenceCurve(clip, srEcb, null);
                AnimationUtility.SetObjectReferenceCurve(clip, imgEcb, keyFrames);
            } else if (targetType == AnimationTargetObjectType.SpriteRendererAndImage) {
                AnimationUtility.SetObjectReferenceCurve(clip, srEcb, keyFrames);
                AnimationUtility.SetObjectReferenceCurve(clip, imgEcb, keyFrames);
            }

            EditorUtility.SetDirty(clip);
            anim.animationClip = clip;
        }

        public void ApplyGlobalFramesToAnimationFrames() {
            for (int i = 0; i < animations.Count; i++) {
                ImportedAnimation anim = animations[i];

                anim.frames = frames.GetRange(anim.firstSpriteIndex, anim.Count).ToArray();
            }
        }

        // ================================================================================
        //  determine looping state of animations
        // --------------------------------------------------------------------------------

        public void SetNonLoopingAnimations(List<string> nonLoopingAnimationNames) {
            Regex nonLoopingAnimationsRegex = GetRegexFromNonLoopingAnimationNames(nonLoopingAnimationNames);

            foreach (var item in animations) {
                item.isLooping = ShouldLoop(nonLoopingAnimationsRegex, item.name);
            }
        }

        private bool ShouldLoop(Regex nonLoopingAnimationsRegex, string name) {
            if (!string.IsNullOrEmpty(nonLoopingAnimationsRegex.ToString())) {
                if (nonLoopingAnimationsRegex.IsMatch(name)) {
                    return false;
                }
            }

            return true;
        }

        private Regex GetRegexFromNonLoopingAnimationNames(List<string> value) {
            string regexString = string.Empty;
            if (value.Count > 0) {
                // Add word boundaries to treat non-regular expressions as exact names
                regexString = string.Concat("\\b", value[0], "\\b");
            }

            for (int i = 1; i < value.Count; i++) {
                string anim = value[i];
                // Add or to speed up the test rather than building N regular expressions
                regexString = string.Concat(regexString, "|", "\\b", anim, "\\b");
            }

            return new System.Text.RegularExpressions.Regex(regexString);
        }

        // ================================================================================
        //  Sprite Data
        // --------------------------------------------------------------------------------
        static Vector2 GetActualPivot(in AnimationImporterSharedConfig.CustomPivotSettings customPivotSettings, float width, float height) {
            switch (customPivotSettings.alignment) {
            case SpriteAlignment.Center: return new Vector2(0.5f, 0.5f);
            case SpriteAlignment.TopLeft: return new Vector2(0, 1);
            case SpriteAlignment.TopCenter: return new Vector2(0.5f, 1);
            case SpriteAlignment.TopRight: return new Vector2(1, 1);
            case SpriteAlignment.LeftCenter: return new Vector2(0, 0.5f);
            case SpriteAlignment.RightCenter: return new Vector2(1, 0.5f);
            case SpriteAlignment.BottomLeft: return new Vector2(0, 0);
            case SpriteAlignment.BottomCenter: return new Vector2(0.5f, 0);
            case SpriteAlignment.BottomRight: return new Vector2(1, 0);
            default: {
                Vector2 custom;
                if (customPivotSettings.useAsepriteCoordinates) {
                    custom.x = customPivotSettings.asepriteX / width;
                    custom.y = 1.0f - (customPivotSettings.asepriteY / height);
                } else {
                    custom.x = customPivotSettings.normalizedX;
                    custom.y = customPivotSettings.normalizedY;
                }
                return custom;
            }
            };
        }
        public void CreateSpriteInfos(in AnimationImporterSharedConfig.CustomPivotSettings customPivotSettings, List<NamedSpriteInfo> spriteInfos) {
            if (frames.Count <= 0) return;
            var pivot = GetActualPivot(in customPivotSettings, frames[0].width, frames[0].height);

            for (int i = 0; i < frames.Count; i++) {
                ImportedAnimationFrame spriteInfo = frames[i];
                spriteInfos.Add(new NamedSpriteInfo {
                    name = spriteInfo.name,
                    info = new Blingame.Importers.SpritePacker.SpriteInfo {
                        tex = this.srcTex,
                        frame = new RectInt(spriteInfo.x, spriteInfo.y, spriteInfo.width, spriteInfo.height),
                        pivotN = pivot
                    }
                });
            }
        }

        public void ApplySpriteNamingScheme(SpriteNamingScheme namingScheme) {
            const string NAME_DELIMITER = "_";

            if (namingScheme == SpriteNamingScheme.Classic) {
                for (int i = 0; i < frames.Count; i++) {
                    frames[i].name = name + " " + i.ToString();
                }
            } else {
                foreach (var anim in animations) {
                    for (int i = 0; i < anim.frames.Length; i++) {
                        var animFrame = anim.frames[i];

                        switch (namingScheme) {
                        case SpriteNamingScheme.FileAnimationZero:
                            animFrame.name = name + NAME_DELIMITER + anim.name + NAME_DELIMITER + i.ToString();
                            break;
                        case SpriteNamingScheme.FileAnimationOne:
                            animFrame.name = name + NAME_DELIMITER + anim.name + NAME_DELIMITER + (i + 1).ToString();
                            break;
                        case SpriteNamingScheme.AnimationZero:
                            animFrame.name = anim.name + NAME_DELIMITER + i.ToString();
                            break;
                        case SpriteNamingScheme.AnimationOne:
                            animFrame.name = anim.name + NAME_DELIMITER + (i + 1).ToString();
                            break;
                        }
                    }
                }
            }

            // remove unused frames from the list so they don't get created for the sprite sheet
            for (int i = frames.Count - 1; i >= 0; i--) {
                if (string.IsNullOrEmpty(frames[i].name)) {
                    frames.RemoveAt(i);
                }
            }
        }

        public void ApplyCreatedSprites(Dictionary<string, Sprite> sprites) {
            if (sprites == null) {
                return;
            }
            foreach (var frame in frames) {
                frame.sprite = sprites[frame.name];
            }
        }

        // ================================================================================
        //  private methods
        // --------------------------------------------------------------------------------

        private void BuildIndex() {
            _animationDatabase = new Dictionary<string, ImportedAnimation>();

            for (int i = 0; i < animations.Count; i++) {
                ImportedAnimation anim = animations[i];
                _animationDatabase[anim.name] = anim;
            }
        }
    }
}
