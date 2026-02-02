using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using System.Collections.Generic;

namespace Unity.Tutorials.Core.Editor
{
    /// <summary>
    /// An index for the tutorials in the project.
    /// </summary>
    public class TutorialContainer : ScriptableObject
    {
        /// <summary>
        /// Raised when any TutorialContainer is modified.
        /// </summary>
        /// <remarks>
        /// Raised before Modified event.
        /// </remarks>
        public static event Action<TutorialContainer> TutorialContainerModified;  // TODO 2.0 merge the two Modified events?

        /// <summary>
        /// Raised when any field of this container is modified.
        /// </summary>
        public event Action Modified;

        /// <summary>
        /// Background texture for the header area that is used to display Title and Subtitle.
        /// </summary>
        public Texture2D HeaderBackground;

        /// <summary>
        /// Title shown in the header area.
        /// </summary>
        [Tooltip("Title shown in the header area.")]
        public LocalizableString Title;

        /// <summary>
        /// Subtitle shown in the header area.
        /// </summary>
        [Tooltip("Subtitle shown in the header area.")]
        public LocalizableString Subtitle;

        /// <summary>
        /// Obsolete currently but might be used when we implement tutorial categories.
        /// </summary>
        [Obsolete, Tooltip("Not applicable currently.")]
        public LocalizableString Description;

        /// <summary>
        /// Can be used to override or disable the default layout specified by the Tutorial Framework.
        /// </summary>
        [Tooltip("Can be used to override or disable the default layout specified by the Tutorial Framework.")]
        public UnityEngine.Object ProjectLayout;

        //lindsay added these, indexed into to get tab indicies
        public int currentTutorialSystemVersion = 0;
        public int currentTutorialContentVersion = 0; //UPDATE THIS IN UNITY WHEN MAKING AN UPDATE LINDSAY
        public string guildID = "851553122811379762";//KIT109_2021_S2, but set this to something different for different years etc
        public string tutorialID; //use this to match the folder names on robo lindsay for updates
        public List<string> tabNames = new List<string>();
        public List<string> tabTooltips = new List<string>();
        //use this to provide option to download a package containing what is needed to start each tutorial
        public List<DefaultAsset> basePackages = new List<DefaultAsset>();


        /// <summary>
        /// Sections/cards of this container.
        /// </summary>
        public Section[] Sections = { };

        //lfwells added these for testing
        public bool showBasePackages = true;
        //public bool debugCompletionCache = false;

        /// <summary>
        /// Returns the path for the ProjectLayout, relative to the project folder,
        /// or a default tutorial layout path if ProjectLayout not specified.
        /// </summary>
        public string ProjectLayoutPath =>
            ProjectLayout != null ? AssetDatabase.GetAssetPath(ProjectLayout) : k_DefaultLayoutPath;

        // The default layout used when a project is started for the first time.
        internal static readonly string k_DefaultLayoutPath =
            "Packages/com.kit109.all-tutorials-framework/Editor/Layouts/DefaultLayout.wlt";

        /// <summary>
        /// A section/card for starting a tutorial or opening a web page.
        /// </summary>
        [Serializable]
        public class Section
        {
            //lindsay added this as a quick hack for kit109 2021s2. This gives us tabs and multiple tute lists per project
            public int kit109Tutorial = -1; //-1 is default, and will always appear regardless of what tab is shown
                                            //note if all tutes are -1, then no tabs shown (default behaviour)

            /// <summary>
            /// Order the the view. Use 0, 2, 4, and so on.
            /// </summary>
            //public int OrderInView; // used to reorder Sections as it's not currently implement as ReorderableList.

            /// <summary>
            /// Title of the card.
            /// </summary>
            public LocalizableString Heading;

            /// <summary>
            /// Description of the card.
            /// </summary>
            public LocalizableString Text;

            /// <summary>
            /// Used as content type metadata for external references/URLs
            /// </summary>
            [Tooltip("Used as content type metadata for external references/URLs"), FormerlySerializedAs("LinkText")]
            public string Metadata;

            /// <summary>
            /// The URL of this section.
            /// Setting the URL will take precedence and make the card act as a link card instead of a tutorial card
            /// </summary>
            [Tooltip("Setting the URL will take precedence and make the card act as a link card instead of a tutorial card")]
            public string Url;

            /// <summary>
            /// Image for the card.
            /// </summary>
            public Texture2D Image;

            /// <summary>
            /// The tutorial (section) that must be finished before this one can be started
            /// </summary>
            [Tooltip("Pre-requesite Tutorial")]
            public Section TutorialThatMustBeCompletedFirst; //TODO: why this no work?
            [Tooltip("Pre-requesite Tutorial")]
            public int IndexOfPrereqSection = -1; //NB: this index is relative to the current tutorial list, not the sections array

            /// <summary>
            /// The tutorial this container contains
            /// </summary>
            public Tutorial Tutorial;


            bool m_Completed;
            /// <summary>
            /// Has the tutorial been already completed?
            /// </summary>
            public bool TutorialCompleted
            {
                get
                {
                    return m_Completed;
                }

                set
                {
                    if (value && m_Completed == false)
                    {
                        _ = RoboAnalytics.SaveTutorialSectionCompleted(this);
                    }
                    m_Completed = value;

                    //Debug.Log("SET Tutorialcompleted tp " + value +" for "+Text);
                }
            }
            public void SetTutorialCompletedWithoutServer()
            {
                //Debug.Log("SetTutorialCompletedWithoutServer " + this.Text);
                m_Completed = true;
            }

            /// <summary>
            /// Does this represent a tutorial?
            /// </summary>
            public bool IsTutorial => Url.IsNullOrEmpty();

            /// <summary>
            /// The ID of the represented tutorial, if any
            /// </summary>
            public string TutorialId => Tutorial?.LessonId.AsEmptyIfNull();

            internal string SessionStateKey => $"Unity.Tutorials.Core.Editor.lesson{TutorialId}";

            /// <summary>
            /// Starts the tutorial of the section
            /// </summary>
            public void StartTutorial()
            {
                TutorialManager.instance.StartTutorial(Tutorial);
            }

            /// <summary>
            /// Opens the URL Of the section, if any
            /// </summary>
            static double lastTimeUrlOpened = 0; //lfwells hax to prevent multi tabs opening on quiz links
            public void OpenUrl()
            {
                if (EditorApplication.timeSinceStartup - lastTimeUrlOpened < 1)
                {
                    return;
                }
                lastTimeUrlOpened = EditorApplication.timeSinceStartup;
                TutorialEditorUtils.OpenUrl(Url);
                AnalyticsHelper.SendExternalReferenceEvent(Url, Heading.Untranslated, Metadata, Tutorial?.LessonId);
            }

            /// <summary>
            /// Loads the state of the section from SessionState.
            /// </summary>
            /// <returns>returns true if the state was found from EditorPrefs</returns>
            public bool LoadState()
            {
                const string nonexisting = "NONEXISTING";
                var state = SessionState.GetString(SessionStateKey, nonexisting);
                if (state == "")
                {
                    TutorialCompleted = false;
                }
                else if (state == "Finished")
                {
                    TutorialCompleted = true;
                }
                return state != nonexisting;
            }

            /// <summary>
            /// Saves the state of the section from SessionState.
            /// </summary>
            public void SaveState()
            {
                SessionState.SetString(SessionStateKey, TutorialCompleted ? "Finished" : "");
            }
        }

        void OnValidate()
        {
            SortSections();
            /*for (int i = 0; i < Sections.Length; ++i)
            {
                Sections[i].OrderInView = i * 2;
            }*/
        }

        void SortSections()
        {
            //Array.Sort(Sections, (x, y) => x.OrderInView.CompareTo(y.OrderInView));
        }

        /// <summary>
        /// Loads the tutorial project layout
        /// </summary>
        public void LoadTutorialProjectLayout()
        {
            TutorialManager.LoadWindowLayoutWorkingCopy(ProjectLayoutPath);
        }

        /// <summary>
        /// Raises the Modified events for this asset.
        /// </summary>
        public void RaiseModifiedEvent()
        {
            TutorialContainerModified?.Invoke(this);
            Modified?.Invoke();
        }
    }
}
