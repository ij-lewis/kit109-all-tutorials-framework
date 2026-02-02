using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Unity.Tutorials.Core.Editor
{
    public static class RoboConfig
    {
        public static string SERVER_LOCATION = "http://131.217.172.176";
        static string GUILD
        {
            get
            {
                var tutorialContainer = TutorialWindow.FindReadme();
                //var tutorialContianer = AssetDatabase.LoadAssetAtPath<TutorialContainer>("Assets/Tutorials/Tutorials.asset");
                return tutorialContainer.guildID;
            }
        }
        public static string PROGRESS_URL
        {
            get
            {
                return SERVER_LOCATION + "/guild/" + GUILD + "/recordProgress/";
            }
        }
        public static string SECTION_PROGRESS_URL
        {
            get
            {
                return SERVER_LOCATION + "/guild/" + GUILD + "/recordSectionProgress/";
            }
        }
        public static string UPDATE_URL
        {
            get
            {
                return SERVER_LOCATION + "/static/tutorialUpdates/";
            }
        }
        public static string PlayerPrefsPrefix
        {
            get
            {
                var tutorialContainer = TutorialWindow.FindReadme();
                return tutorialContainer.tutorialID;
            }
        }
    }
}
