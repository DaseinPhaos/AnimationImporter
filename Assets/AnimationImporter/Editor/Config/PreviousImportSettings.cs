using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using Random = UnityEngine.Random;
using UnityEditor;

namespace AnimationImporter
{
    public class PreviousImportSettings
    {
        // ================================================================================
        //  analyzing animations
        // --------------------------------------------------------------------------------

        public static AnimationTargetObjectType GetAnimationTargetFromExistingClip(AnimationClip clip)
        {
            var curveBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);

            bool targetingSpriteRenderer = false;
            bool targetingImage = false;

            for (int i = 0; i < curveBindings.Length; i++)
            {
                if (curveBindings[i].type == typeof(SpriteRenderer))
                {
                    targetingSpriteRenderer = true;
                }
                else if (curveBindings[i].type == typeof(UnityEngine.UI.Image))
                {
                    targetingImage = true;
                }
            }

            if (targetingSpriteRenderer && targetingImage)
            {
                return AnimationTargetObjectType.SpriteRendererAndImage;
            }
            else if (targetingImage)
            {
                return AnimationTargetObjectType.Image;
            }
            else
            {
                return AnimationTargetObjectType.SpriteRenderer;
            }
        }
    }
}