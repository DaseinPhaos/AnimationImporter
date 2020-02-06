using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using System.IO;

namespace AnimationImporter
{
    public class AnimationImportJob
    {
        private string _assetPath;

        public string name { get { return Path.GetFileNameWithoutExtension(fileName); } }
        public string fileName { get { return Path.GetFileName(_assetPath); } }
        public string assetDirectory { get { return GetBasePath(_assetPath); } }

        private string _directoryPathForSprites = "";
        public string directoryPathForSprites
        {
            get
            {
                if (!Directory.Exists(_directoryPathForSprites))
                {
                    Directory.CreateDirectory(_directoryPathForSprites);
                }

                return _directoryPathForSprites;
            }
            set
            {
                _directoryPathForSprites = value;
            }
        }

        private string _directoryPathForAnimations = "";
        public string directoryPathForAnimations
        {
            get
            {
                if (!Directory.Exists(_directoryPathForAnimations))
                {
                    Directory.CreateDirectory(_directoryPathForAnimations);
                }

                return _directoryPathForAnimations;
            }
            set
            {
                _directoryPathForAnimations = value;
            }
        }

        private string _directoryPathForAnimationControllers = "";
        public string directoryPathForAnimationControllers
        {
            get
            {
                if (!Directory.Exists(_directoryPathForAnimationControllers))
                {
                    Directory.CreateDirectory(_directoryPathForAnimationControllers);
                }

                return _directoryPathForAnimationControllers;
            }
            set
            {
                _directoryPathForAnimationControllers = value;
            }
        }

        public string imageAssetFilename
        {
            get
            {
                return directoryPathForSprites + "/" + name + ".png";
            }
        }

        // additional import settings
        public string additionalCommandLineArguments = null;
        public bool createUnityAnimations = true;
        public ImportAnimatorController importAnimatorController = ImportAnimatorController.None;
        public bool useExistingAnimatorController = false;

        // ================================================================================
        //  constructor
        // --------------------------------------------------------------------------------

        public AnimationImportJob(string assetPath)
        {
            _assetPath = assetPath;
        }

        // ================================================================================
        //  progress
        // --------------------------------------------------------------------------------

        public delegate void ProgressUpdatedDelegate(float progress, string msg);
        public event ProgressUpdatedDelegate progressUpdated;

        float _progress = 0;
        string lastMsg = "";

        public void SetProgress(float progress)
        {
            SetProgress(progress, lastMsg);
        }

        public void SetProgress(float progress, string msg)
        {
            this._progress = progress;
            this.lastMsg = msg;

            if (progressUpdated != null)
            {
                progressUpdated(_progress, msg);
            }
        }

        // ================================================================================
        //  private methods
        // --------------------------------------------------------------------------------
        private string GetBasePath(string path)
        {
            string extension = Path.GetExtension(path);
            if (extension.Length > 0 && extension[0] == '.')
            {
                extension = extension.Remove(0, 1);
            }

            string fileName = Path.GetFileNameWithoutExtension(path);
            string lastPart = "/" + fileName + "." + extension;

            return path.Replace(lastPart, "");
        }
    }
}