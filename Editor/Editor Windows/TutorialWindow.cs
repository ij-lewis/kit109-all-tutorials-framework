using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using static Unity.Tutorials.Core.Editor.RichTextParser;
using UnityObject = UnityEngine.Object;

namespace Unity.Tutorials.Core.Editor
{
    using static Localization;

    /// <summary>
    /// The window used to display all tutorial content.
    /// </summary>
    public sealed class TutorialWindow : EditorWindowProxy
    {
        //lindsay added
        int m_currentTab = -1;
        int CurrentTab
        {
            get
            {
                if (m_currentTab == -1 && Tabs.Count > 0) m_currentTab = PlayerPrefs.GetInt(RoboConfig.PlayerPrefsPrefix + "CurrentTab", 0);
                if (m_currentTab >= Tabs.Count) return -1;
                return m_currentTab;
            }
            set
            {
                if (value >= Tabs.Count) m_currentTab = -1;
                else m_currentTab = value;

                PlayerPrefs.SetInt(RoboConfig.PlayerPrefsPrefix + "CurrentTab", m_currentTab);
            }
        }

        /// <summary>
        /// Should we show the Close Tutorials info dialog for the user for the current project.
        /// By default the dialog is shown once per project and disabled after that.
        /// </summary>
        /// <remarks>
        /// You want to set this typically to false when running unit tests.
        /// </remarks>
        public static ProjectSetting<bool> ShowTutorialsClosedDialog =
            new ProjectSetting<bool>("IET.ShowTutorialsClosedDialog", "Show info dialog when the window is closed", true);

        VisualElement m_VideoBoxElement;

        const int k_MinWidth = 300;
        const int k_MinHeight = 300;
        internal static readonly string k_UIAssetPath = "Packages/com.kit109.all-tutorials-framework/Editor/UI";

        // Loads an asset from a common UI resource folder.
        internal static T LoadUIAsset<T>(string filename) where T : UnityObject =>
            AssetDatabase.LoadAssetAtPath<T>($"{k_UIAssetPath}/{filename}");

        SystemLanguage m_CurrentEditorLanguage; // uninitialized in order to force translation when the window is enabled for the first time

        List<TutorialParagraph> m_Paragraphs = new List<TutorialParagraph>();
        int[] m_Indexes;
        [SerializeField]
        List<TutorialParagraph> m_AllParagraphs = new List<TutorialParagraph>();

        internal static readonly float s_AuthoringModeToolbarButtonWidth = 115;

        static readonly bool s_AuthoringMode = ProjectMode.IsAuthoringMode();

        string m_NextButtonText = "";
        string m_BackButtonText = "";
        string m_WindowTitleContent;
        string m_HomePromptTitle; // TODO unused currently
        string m_HomePromptText; // TODO unused currently
        string m_PromptYes; // TODO unused currently
        string m_PromptNo; // TODO unused currently
        string m_PromptOk;
        string m_MenuPathGuide;
        string m_TabClosedDialogTitle;
        string m_TabClosedDialogText;
        bool m_IsInitialized;

        static bool forceReshowUsernameID = false;

        internal Tutorial currentTutorial;

        /// <summary>
        /// Creates the window if it does not exist, anchoring it as a tab next to the Inspector.
        /// If the window exists already, it's simply brought to the foreground and focused without any other actions.
        /// If Inspector is not visible currently, Tutorials window is will be shown as a free-floating window.
        /// </summary>
        /// <remarks>
        /// This is the new and preferred way to show the Tutorials window.
        /// </remarks>
        /// <returns></returns>
        internal static TutorialWindow CreateNextToInspector()
        {
            var inspectorWindow = Resources.FindObjectsOfTypeAll<EditorWindow>()
                .FirstOrDefault(wnd => wnd.GetType().Name == "InspectorWindow");

            Type windowToAnchorTo = inspectorWindow != null ? inspectorWindow.GetType() : null;
            bool alreadyCreated = EditorWindowUtils.FindOpenInstance<TutorialWindow>() != null;
            // If Inspector not visible/opened, Tutorials window will be created as a free-floating window
            var tutorialWindow = GetOrCreateWindow(windowToAnchorTo); // create & anchor or simply focus
            if (alreadyCreated)
                return tutorialWindow;

            if (inspectorWindow)
                inspectorWindow.DockWindow(tutorialWindow, EditorWindowUtils.DockPosition.Right);

            return tutorialWindow;
        }

        /// <summary>
        /// Creates the window if it does not exist, and positions it using a window layout
        /// specified either by the project's TutorialContainer or Tutorial Framework's default layout.
        /// If the window exists already, it's simply brought to the foreground and focused without any other actions.
        /// If the project layout does not contain Tutorials window, it will be shown as a free-floating window.
        /// </summary>
        /// <remarks>
        /// This is the old way to show the Tutorials window and should be preferred only in situations where
        /// a special window layout is preferred when starting a tutorial project for the first time.
        /// </remarks>
        /// <returns></returns>
        internal static TutorialWindow CreateWindowAndLoadLayout()
        {
            var tutorialWindow = EditorWindowUtils.FindOpenInstance<TutorialWindow>();
            if (tutorialWindow != null)
                return GetOrCreateWindow(); // focus

            var readme = FindReadme();
            if (readme != null)
                readme.LoadTutorialProjectLayout();

            // If project layout did not contain tutorial window, it will be created as a free-floating window
            tutorialWindow = EditorWindowUtils.FindOpenInstance<TutorialWindow>();
            if (tutorialWindow == null)
                tutorialWindow = GetOrCreateWindow(); // create

            return tutorialWindow;
        }

        /// <summary>
        /// Creates a window and positions it as a tab of another window, if wanted.
        /// If the window exists already, it's brought to the foreground and focused.
        /// </summary>
        /// <param name="windowToAnchorTo"></param>
        /// <returns></returns>
        internal static TutorialWindow GetOrCreateWindow(Type windowToAnchorTo = null)
        {
            var window = GetWindow<TutorialWindow>(windowToAnchorTo);
            window.minSize = new Vector2(k_MinWidth, k_MinHeight);

            return window;
        }

        internal TutorialContainer readme
        {
            get { return m_Readme; }
            set
            {
                if (m_Readme)
                    m_Readme.Modified -= OnTutorialContainerModified;

                var oldReadme = m_Readme;
                m_Readme = value;
                if (m_Readme)
                {
                    if (oldReadme != m_Readme)
                        FetchTutorialStates();

                    m_Readme.Modified += OnTutorialContainerModified;
                }
            }
        }
        [SerializeField]
        TutorialContainer m_Readme;

        TutorialContainer.Section[] Sections => readme?.Sections ?? new TutorialContainer.Section[0];
        List<string> Tabs => readme?.tabNames ?? new List<string>();
        List<string> TabTooltips => readme?.tabTooltips ?? new List<string>();
        List<DefaultAsset> TabBasePackages => readme?.basePackages ?? new List<DefaultAsset>();

        class Card
        {
            public TutorialContainer.Section Section;
            public VisualElement Element;
        }

        [SerializeField]
        Card[] m_Cards = { };

        bool CanMoveToNextPage
        {
            get
            {
                return currentTutorial != null && currentTutorial.CurrentPage != null &&
                    (currentTutorial.CurrentPage.AreAllCriteriaSatisfied ||
                        currentTutorial.CurrentPage.HasMovedToNextPage);
            }
        }

        bool MaskingEnabled
        {
            get
            {
                return MaskingManager.MaskingEnabled && (m_MaskingEnabled || !s_AuthoringMode);
            }
            set { m_MaskingEnabled = value; }
        }
        [SerializeField]
        bool m_MaskingEnabled = true;

        TutorialStyles Styles { get { return TutorialProjectSettings.Instance.TutorialStyle; } }

        [SerializeField]
        int m_FarthestPageCompleted = -1;

        [SerializeField]
        bool m_PlayModeChanging;

        VideoPlaybackManager VideoPlaybackManager { get; } = new VideoPlaybackManager();
        Texture m_VideoTextureCache;

        bool m_DoneFetchingTutorialStates = false;

        void OnTutorialContainerModified()
        {
            // Update the tutorial content in real-time when changed
            // TODO we end up reinitializing the whole UI when editing a single field of TutorialContainer.
            // Implement more granular updates.
            if (currentTutorial == null)
                InitializeUI();
        }

        void TrackPlayModeChanging(PlayModeStateChange change)
        {
            switch (change)
            {
                case PlayModeStateChange.ExitingEditMode:
                case PlayModeStateChange.ExitingPlayMode:
                    m_PlayModeChanging = true;
                    break;
                case PlayModeStateChange.EnteredEditMode:
                case PlayModeStateChange.EnteredPlayMode:
                    m_PlayModeChanging = false;
                    break;
            }
        }

        void OnFocus()
        {
            readme = FindReadme(); // TODO could this be removed?
        }

        void UpdateVideoFrame(Texture newTexture)
        {
            rootVisualElement.Q("TutorialMedia").style.backgroundImage = Background.FromTexture2D((Texture2D)newTexture);
        }

        void UpdateHeader(TextElement contextText, TextElement titleText, VisualElement backDrop)
        {
            bool hasTutorial = currentTutorial != null;
            var context = readme != null ? readme.Subtitle.Value : string.Empty;
            var title = hasTutorial ? currentTutorial.TutorialTitle.Value : readme?.Title.Value;
            // For now drawing header only for Readme
            if (readme)
            {
                contextText.text = context;
                titleText.text = title;

                backDrop.style.backgroundImage = readme.HeaderBackground;
            }
        }

        void ScrollToTop()
        {
            try
            {
                ((ScrollView)this.rootVisualElement.Q("TutorialContainer").ElementAt(0)).scrollOffset = Vector2.zero;
            }
            catch (Exception) { }
        }

        void ShowCurrentTutorialContent()
        {
            if (!m_AllParagraphs.Any() || !currentTutorial)
                return;
            if (m_AllParagraphs.Count() <= currentTutorial.CurrentPageIndex)
                return;

            ScrollToTop();

            TutorialParagraph instruction = null;
            TutorialParagraph narrative = null;
            Tutorial endLink = null;
            string endText = "";

            int instructionsCount = 0;
            int narrativesCount = 0;

            ScrollView scrollView;
            try
            {
                scrollView = rootVisualElement.Q<VisualElement>("TutorialContainer").Q<ScrollView>();
            }
            catch (Exception) { return; }

            // clear all of the paragraphs from the previous page
            VisualElement element;
            while ((element = scrollView.Q("InstructionContainer")) != null)
            {
                scrollView.Remove(element);
            }
            while ((element = scrollView.Q("TutorialStepBox1")) != null)
            {
                scrollView.Remove(element);
            }
            while ((element = scrollView.Q("PollContainer")) != null)
            {
                scrollView.Remove(element);
            }
            while ((element = scrollView.Q("TutorialMediaContainer")) != null)
            {
                scrollView.Remove(element);
            }

            // need to hide the media container by default as we can now have para lists that don't have an image para
            HideElement("TutorialMediaContainer");

            foreach (TutorialParagraph para in currentTutorial.CurrentPage.Paragraphs)
            {
                //Debug.Log("TYPE: " + para.Type);

                if (para.Type == ParagraphType.SwitchTutorial)
                {
                    endLink = para.m_Tutorial;
                    endText = para.Text;
                }
                if (para.Type == ParagraphType.Narrative)
                {
                    narrative = para;

                    var narrativeElement = tutorialContentAssetCached.CloneTree().Q<VisualElement>("TutorialStepBox1");

                    scrollView.Add(narrativeElement);

                    if (narrativesCount == 0)
                    {
                        rootVisualElement.Q<Label>("TutorialTitle").text = narrative.Title;
                    }

                    RichTextToVisualElements(narrative.Text, narrativeElement);

                    narrativesCount++;
                }
                if (para.Type == ParagraphType.Instruction)
                {
                    instruction = para;

                    var instructionElement = tutorialContentAssetCached.CloneTree().Q<VisualElement>("InstructionContainer");

                    scrollView.Add(instructionElement);

                    var title = instructionElement.Q<Label>("InstructionTitle");
                    var desc = instructionElement.Q<VisualElement>("InstructionDescription");

                    if (string.IsNullOrEmpty(instruction.Title))
                        HideElement(instructionElement.Q("InstructionTitle"));
                    else
                        ShowElement(instructionElement.Q("InstructionTitle"));
                    title.text = instruction.Title;
                    RichTextToVisualElements(instruction.Text, desc);

                    int newLineCount = instruction.Text.Value.Split('\n').Length;
                    //IFF there is a code-block in this, pull out the text from that and subtract away the number of new lines in the code block
                    if (instruction.Text.Value.Contains("<code>"))
                    {
                        var codeBlock = instruction.Text.Value.Substring(instruction.Text.Value.IndexOf("<code>") + 6, instruction.Text.Value.IndexOf("</code>") - instruction.Text.Value.IndexOf("<code>") - 6);
                        newLineCount -= codeBlock.Split('\n').Length;
                    }
                    desc.style.marginBottom = newLineCount * 10;

                    // show the "I've completed this step" check-box if that is selected as the completion type
                    var checkbox = instructionElement.Q<Toggle>("InstructionUserCheckbox");
                    if (para.m_CriteriaCompletion == CompletionType.CompletedOnUserIndication || para.m_CriteriaCompletion == CompletionType.CompletedOnUserIndicationSkippable)
                    {
                        ShowElement(checkbox);

                        checkbox.value = para.UserIndicatedDone;
                        UpdatePageState();

                        checkbox.RegisterCallback<ClickEvent>((evt) =>
                        {
                            para.UserIndicatedDone = (evt.target as Toggle).value;
                            UpdateInstructionBox();
                            UpdatePageState();
                        });
                    }
                    else
                    {
                        HideElement(checkbox);
                    }

                    var checkboxSkip = instructionElement.Q<Toggle>("InstructionUserCheckboxSkip");
                    if (para.m_CriteriaCompletion == CompletionType.CompletedOnUserIndicationSkippable)
                    {
                        ShowElement(checkboxSkip);

                        checkboxSkip.value = para.UserIndicatedSkipped;
                        UpdatePageState();

                        checkboxSkip.RegisterCallback<ClickEvent>((evt) =>
                        {
                            para.UserIndicatedSkipped = (evt.target as Toggle).value;
                            UpdateInstructionBox();
                            UpdatePageState();
                        });
                    }
                    else
                    {
                        HideElement(checkboxSkip);
                    }


                    // Error UI handling
                    var errorHeader = instructionElement.Q<VisualElement>("InstructionErrorHeader");
                    var currentInstruction = instruction; // Fix closure capture
                    if (errorHeader != null)
                    {
                        errorHeader.RegisterCallback<ClickEvent>(evt =>
                        {
                            UnityEngine.Debug.Log($"Error Header Clicked. Old state: {currentInstruction.ErrorExpanded}");
                            currentInstruction.ErrorExpanded = !currentInstruction.ErrorExpanded;
                            UpdateInstructionBox();
                            UnityEngine.Debug.Log($"New state: {currentInstruction.ErrorExpanded}");
                            evt.StopPropagation();
                        });
                    }

                    instructionsCount++;
                }

                // custom paragraph type for having quick polls (select any option to prgoress, but get shown correct response too)
                if (para.Type == ParagraphType.Poll)
                {
                    var pollElement = tutorialContentAssetCached.CloneTree().Q<VisualElement>("PollContainer");

                    scrollView.Add(pollElement);

                    var title = pollElement.Q<Label>("PollTitle");
                    var desc = pollElement.Q<VisualElement>("PollDescription");

                    if (string.IsNullOrEmpty(para.Title))
                        HideElement(pollElement.Q("PollTitle"));
                    else
                        ShowElement(pollElement.Q("PollTitle"));
                    title.text = para.Title;
                    RichTextToVisualElements(para.Text, desc);

                    for (int i = 0; i < 4; i++) //TODO: don't have a hard-coded limit on the number of options
                    {
                        int iCopy = i;//otherwise the chosen poll option will always be 4
                        var toggle = pollElement.Q<Toggle>("PollOption" + (i + 1));

                        //Debug.Log(toggle + " " + para.m_PollItems[i]);

                        if (toggle != null && i < para.m_PollItems.Count)
                        {
                            ShowElement(toggle);
                            toggle.text = para.m_PollItems[i];

                            toggle.RegisterCallback<ClickEvent>((evt) =>
                            {
                                for (int i = 0; i < 4; i++) //TODO: don't have a hard-coded limit on the number of options
                                {
                                    var other = pollElement.Q<Toggle>("PollOption" + (i + 1));

                                    if (other != toggle) other.value = false;
                                }

                                para.UserIndicatedDone = (evt.target as Toggle).value;
                                para.ChosenPollOption = iCopy;
                                //Debug.Log("Para: " + para.Title + " " + para.UserIndicatedDone);
                                UpdateInstructionBox();
                                UpdatePageState();
                            });

                            if (para.m_PollCorrectItemIndexes.Contains(i))
                            {
                                toggle.AddToClassList("correct");
                                toggle.RemoveFromClassList("wrong");
                            }
                            else
                            {
                                toggle.RemoveFromClassList("correct");
                                toggle.AddToClassList("wrong");
                            }

                            if (para.UserIndicatedDone && para.ChosenPollOption == i)
                            {
                                toggle.AddToClassList("checked");
                            }
                            else
                            {
                                toggle.RemoveFromClassList("checked");
                            }
                        }
                        else
                        {
                            HideElement(toggle);
                        }
                    }
                }
                if (para.Type == ParagraphType.Image)
                {
                    if (para.Image != null)
                    {
                        var imageElement = tutorialContentAssetCached.CloneTree().Q<VisualElement>("TutorialMediaContainer");
                        scrollView.Add(imageElement);

                        imageElement.style.backgroundImage = para.Image;
                        if (para.Image.width * 2 < 300)
                        {
                            imageElement.style.minHeight = para.Image.height * 2;
                        }
                        else
                        {
                            imageElement.style.minHeight = para.Image.height;
                        }
                        //imageElement.style.minWidth = para.Image.width;

                        if (para.disableImagePopup == false)
                        {
                            imageElement.AddManipulator(new Clickable(() =>
                            {
                                ImageWindow.Open(para.Image);
                            }));
                        }
                    }
                }
                if (para.Type == ParagraphType.Video)
                {
                    if (para.Video != null)
                    {
                        ShowElement("TutorialMediaContainer");
                        rootVisualElement.Q("TutorialMedia").style.backgroundImage = VideoPlaybackManager.GetTextureForVideoClip(para.Video);
                    }
                    else
                    {
                        HideElement("TutorialMediaContainer");
                    }
                }
            }

            UpdateInstructionBox();
            UpdatePageState();

            Button linkButton = rootVisualElement.Q<Button>("LinkButton");
            if (endLink != null)
            {
                linkButton.clickable = new Clickable(() => StartEndLinkTutorial(endLink));
                linkButton.text = Tr(endText);
                ShowElement(linkButton);
            }
            else
            {
                HideElement(linkButton);
            }

            if (IsFirstPage())
            {
                ShowElement("NextButtonBase");
            }
            else
            {
                HideElement("NextButtonBase");
            }
        }

        void StartEndLinkTutorial(Tutorial endLink)
        {
            TutorialManager.instance.IsTransitioningBetweenTutorials = true;
            TutorialManager.instance.StartTutorial(endLink);
        }

        // Sets the instruction highlight to green or blue and toggles between arrow and checkmark
        void UpdateInstructionBox()
        {
            //bool CanMoveToNextPage =>
            //currentTutorial != null && currentTutorial.CurrentPage != null &&
            //(currentTutorial.CurrentPage.AreAllCriteriaSatisfied ||
            //    currentTutorial.CurrentPage.HasMovedToNextPage);

            //TODO: I haven't used currentTutorial.CurrentPage.HasMovedToNextPage in this, because I'm not quite sure what I should do with it

            if (currentTutorial != null && currentTutorial.CurrentPage != null)
            {
                int instructionsCount = 0;
                var instructionElements = rootVisualElement.Query<VisualElement>("InstructionContainer").ToList(); // because apparently using AtIndex traverses the tree each time
                int pollsCount = 0;
                var pollElements = rootVisualElement.Query<VisualElement>("PollContainer").ToList(); // because apparently using AtIndex traverses the tree each time
                foreach (var paragraph in currentTutorial.CurrentPage.Paragraphs)
                {
                    if (paragraph.Type == ParagraphType.Instruction)
                    {
                        var instructionElement = instructionElements[instructionsCount]; // rootVisualElement.Q<VisualElement>(name);

                        if (paragraph.Completed)
                        {
                            ShowElement(instructionElement.Q("InstructionHighlightGreen"));
                            HideElement(instructionElement.Q("InstructionHighlightBlue"));
                            ShowElement(instructionElement.Q("InstructionCheckmark"));
                            HideElement(instructionElement.Q("InstructionArrow"));
                        }
                        else
                        {
                            HideElement(instructionElement.Q("InstructionHighlightGreen"));
                            ShowElement(instructionElement.Q("InstructionHighlightBlue"));
                            HideElement(instructionElement.Q("InstructionCheckmark"));
                            ShowElement(instructionElement.Q("InstructionArrow"));
                        }

                        if (paragraph.m_CriteriaCompletion == CompletionType.CompletedOnUserIndication || paragraph.m_CriteriaCompletion == CompletionType.CompletedOnUserIndicationSkippable)
                            ShowElement(instructionElement.Q("InstructionUserCheckbox"));
                        else
                            HideElement(instructionElement.Q("InstructionUserCheckbox"));

                        if (paragraph.m_CriteriaCompletion == CompletionType.CompletedOnUserIndicationSkippable)
                            ShowElement(instructionElement.Q("InstructionUserCheckboxSkip"));
                        else
                            HideElement(instructionElement.Q("InstructionUserCheckboxSkip"));

                        var eText = paragraph.ErrorTutorialText;
                        var errorContainer = instructionElement.Q<VisualElement>("InstructionErrorContainer");
                        var errorHeaderLabel = instructionElement.Q<Label>("InstructionErrorHeaderLabel");
                        var errorDetailLabel = instructionElement.Q<Label>("InstructionErrorDetailLabel");

                        if (!string.IsNullOrEmpty(eText))
                        {
                            ShowElement(errorContainer);
                            if (paragraph.ErrorExpanded)
                            {
                                errorHeaderLabel.text = "Errors found: Click here to hide info"; // Localizable? Keeping hardcoded as per request for now
                                ShowElement(errorDetailLabel);
                                errorDetailLabel.text = eText;
                            }
                            else
                            {
                                errorHeaderLabel.text = "Errors found: click here to show more information";
                                HideElement(errorDetailLabel);
                            }
                        }
                        else
                        {
                            HideElement(errorContainer);
                        }
                        
                        instructionsCount++;
                    }

                    // custom poll paragraph type
                    if (paragraph.Type == ParagraphType.Poll) //TODO: this is pretty ugly, how much of this can be combined with the previous?
                    {
                        if (pollsCount < pollElements.Count)
                        {
                            var pollElement = pollElements[pollsCount]; // rootVisualElement.Q<VisualElement>(name);

                            if (paragraph.Completed)
                            {
                                ShowElement(pollElement.Q("InstructionHighlightGreen"));
                                HideElement(pollElement.Q("InstructionHighlightBlue"));
                                ShowElement(pollElement.Q("InstructionCheckmark"));
                                HideElement(pollElement.Q("InstructionArrow"));

                                pollElement.AddToClassList("answered-poll");
                            }
                            else
                            {
                                HideElement(pollElement.Q("InstructionHighlightGreen"));
                                ShowElement(pollElement.Q("InstructionHighlightBlue"));
                                HideElement(pollElement.Q("InstructionCheckmark"));
                                ShowElement(pollElement.Q("InstructionArrow"));

                                pollElement.RemoveFromClassList("answered-poll");
                            }

                            for (int i = 0; i < 4; i++) //TODO: don't have a hard-coded limit on the number of options
                            {
                                var toggle = pollElement.Q<Toggle>("PollOption" + (i + 1));

                                //Debug.Log(toggle + " " + para.m_PollItems[i]);

                                if (toggle != null && i < paragraph.m_PollItems.Count)
                                {
                                    if (paragraph.UserIndicatedDone && paragraph.ChosenPollOption == i)
                                    {
                                        toggle.AddToClassList("checked");
                                    }
                                    else
                                    {
                                        toggle.RemoveFromClassList("checked");
                                    }
                                }
                            }

                            pollsCount++;
                        }
                    }

                }
            }
        }

        void UpdatePageState()
        {
            // TODO delayCall needed for now as some criteria don't have up-to-date state when at the moment
            // we call this function, causing canMoveToNextPage to return false even though the criteria
            // are completed.
            EditorApplication.delayCall += () =>
            {
                UpdateInstructionBox();
                if (currentTutorial != null && currentTutorial.CurrentPage != null)
                {
                    currentTutorial.CurrentPage.ValidateCriteria();
                }
                SetNextButtonEnabled(CanMoveToNextPage);
            };
        }

        void OnCriterionCompleted(Criterion criterion)
        {
            // The criterion might be non-pertinent for the window (e.g. when running unit tests)
            // TODO Ideally we'd subscribe only to the criteria of the current page so we don't need to check this
            if (!currentTutorial ||
                !currentTutorial.Pages
                    .SelectMany(page => page.Paragraphs)
                    .SelectMany(para => para.Criteria)
                    .Select(crit => crit.Criterion)
                    .Contains(criterion)
            )
            {
                return;
            }

            UpdatePageState();
        }

        bool nextButtonDisabled = true;
        void SetNextButtonEnabled(bool enable)
        {
            nextButtonDisabled = !enable;
            if (enable) rootVisualElement.Q("NextButton").RemoveFromClassList("disabled");
            else rootVisualElement.Q("NextButton").AddToClassList("disabled");
            //rootVisualElement.Q("NextButton").SetEnabled(enable);
        }

        void CreateTabs(VisualTreeAsset vistree, VisualElement cardContainer)
        {
            for (var i = 0; i < Tabs.Count; i++)
            {
                var element = vistree.CloneTree().Q<Button>("TutorialListTab");
                element.text = Tabs[i];
                element.tooltip = i < TabTooltips.Count ? TabTooltips[i] : "";
                int i2 = i;//closure
                element.clicked += () =>
                {
                    CurrentTab = i2;
                    InitializeUI();
                };
                if (i == CurrentTab)
                {
                    element.AddToClassList("current");
                }
                cardContainer.Add(element);
            }
        }
        void CreateTutorialMenuCards(VisualTreeAsset vistree, VisualElement cardContainer, int currentTab)
        {
            m_Cards = Sections
                //.OrderBy(section => section.OrderInView)
                .Where(section => section.kit109Tutorial == -1 || section.kit109Tutorial == currentTab)
                .Select(section => new Card() { Section = section })
                .ToArray();

            if (m_Cards.Any())
            {
                LoadTutorialStates();
                FetchTutorialStates();
                EditorCoroutineUtility.StartCoroutineOwnerless(UpdateCheckmarksWhenStatesFetched());
            }

            foreach (var card in m_Cards)
            {
                var section = card.Section;
                card.Element = vistree.CloneTree().Q("TutorialsContainer").Q(section.IsTutorial ? "CardContainer" : "LinkCardContainer");
                var element = card.Element;
                element.Q<Label>("TutorialName").text = section.Heading;
                element.Q<Label>("TutorialDescription").text = section.Text;
                element.tooltip = section.IsTutorial ? Tr("Tutorial: ") + section.Text : section.Url;
                if (section.Image != null)
                    element.Q("TutorialImage").style.backgroundImage = Background.FromTexture2D(section.Image);

                // NOTE Setting up the checkmark at this point might be futile as we just requested the states from the backend.
                UpdateCheckmark(card);


                cardContainer.Add(element);
            }
        }

        IEnumerator UpdateCheckmarksWhenStatesFetched()
        {
            while (!m_DoneFetchingTutorialStates)
                yield return null;

            foreach (var card in m_Cards)
                UpdateCheckmark(card);
        }

        void UpdateCheckmark(Card card)
        {
            var completed = string.IsNullOrEmpty(card.Section.Url) && !card.Section.Heading.Value.Equals("Homework") ? card.Section.TutorialCompleted : false;
            //Debug.Log("completed:" + completed);
            var pageCount = card?.Section?.Tutorial?.PageCount ?? 0;
            card.Element.Q<Label>("CompletionStatus").text = completed ? Tr("COMPLETED") : (pageCount == 0 ? "" : string.Format("{0} {1}", pageCount, pageCount == 1 ? "PAGE" : "PAGES"));
            SetElementVisible(card.Element.Q("TutorialCheckmark"), completed);

            var section = card.Section;
            var element = card.Element;
            int index = section.IndexOfPrereqSection;
            bool prereqsMet = (index < 0 || (index < m_Cards.Length && m_Cards[section.IndexOfPrereqSection].Section.TutorialCompleted));

            if (section.IsTutorial)
            {
                if (prereqsMet || section.TutorialCompleted || section.Heading.Translated.Contains("Exercise") || section.Heading.Translated.Contains("Homework") || section.Heading.Translated.Contains("Quiz"))
                {
                    element.RegisterCallback(async (MouseUpEvent evt) =>
                    {
                        if (evt.shiftKey)
                        {
                            EditorUtility.DisplayProgressBar("Updating Progress...", "Saving Progress, this might take up to a minute...", 0.5f);
                            //Debug.Log("set complete: " + section.Text);
                            section.SetTutorialCompletedWithoutServer();//TutorialCompleted = true;
                            section.SaveState();
                            await RoboAnalytics.SaveTutorialSectionCompleted(section);
                            InitializeUI();
                            EditorUtility.ClearProgressBar();
                        }
                        else
                        {
                            section.StartTutorial();
                        }
                    });
                    element.SetEnabled(true);
                    element.RemoveFromClassList("disabled");
                }
                else
                {
                    //Debug.Log("setting card disabled: "+section.Text + "completed = "+section.TutorialCompleted);
                    element.RegisterCallback(async (MouseUpEvent evt) =>
                    {
                        if (evt.shiftKey)
                        {
                            var i = 0;
                            foreach (var s in Sections) // Array.FindAll(Sections, s => s.TutorialId == section.TutorialId))
                            {
                                if (s.TutorialCompleted && s != section) continue;
                                i++;
                                EditorUtility.DisplayProgressBar("Updating Progress...", "Saving Progress, this might take up to a minute...", (float)i / (float)Sections.Length);
                                //Debug.Log("set complete2: " + s.Text);
                                s.SetTutorialCompletedWithoutServer();// = true;
                                s.SaveState();
                                await RoboAnalytics.SaveTutorialSectionCompleted(s);
                                if (s == section) break;
                            }
                            InitializeUI();
                            EditorUtility.ClearProgressBar();
                        }
                    });
                    element.AddToClassList("disabled");
                    //element.SetEnabled(false);
                }
            }
            if (!string.IsNullOrEmpty(section.Url))
            {
                //if (prereqsMet)
                //{
                AnalyticsHelper.SendExternalReferenceImpressionEvent(section.Url, section.Heading.Untranslated, section.Metadata, section.TutorialId);

                element.RegisterCallback((MouseUpEvent evt) =>
                {
                    section.OpenUrl();
                });
                element.SetEnabled(true);
                /*}
                else
                {
                    element.SetEnabled(false);
                }*/
            }
        }

        void RenderVideoIfPossible()
        {
            // Possible media is always at the first paragraph.
            var paragraph = currentTutorial?.CurrentPage?.Paragraphs.FirstOrDefault();
            if (paragraph == null)
                return;

            switch (paragraph.Type)
            {
                case ParagraphType.Image:
                    // TODO currently draws image all the time - let's draw it once for each page
                    m_VideoTextureCache = paragraph.Image;
                    UpdateVideoFrame(m_VideoTextureCache);
                    break;
                case ParagraphType.Video:
                    if (paragraph.Video != null)
                    {
                        m_VideoTextureCache = VideoPlaybackManager.GetTextureForVideoClip(paragraph.Video);
                        UpdateVideoFrame(m_VideoTextureCache);
                        Repaint();
                    }
                    break;
            }

        }

        void OnEnable()
        {
            EditorCoroutineUtility.StartCoroutineOwnerless(DeferredOnEnable());
        }

        IEnumerator DeferredOnEnable()
        {
            m_IsInitialized = false;
            m_CurrentEditorLanguage = LocalizationDatabaseProxy.currentEditorLanguage;

            if (EditorApplication.isPlaying)
            {
                yield return null;
            }
            else
            {
                yield return new EditorWaitForSeconds(0.5f);
            }

            AssetDatabase.Refresh();
            AddCallbacksToEvents();
            InitializeUI();
            m_IsInitialized = true;
        }

        void AddCallbacksToEvents()
        {
            Criterion.CriterionCompleted += OnCriterionCompleted;

            // test for page completion state changes (rather than criteria completion/invalidation directly)
            // so that page completion state will be up-to-date
            TutorialPage.CriteriaCompletionStateTested += OnTutorialPageCriteriaCompletionStateTested;
            TutorialPage.TutorialPageMaskingSettingsChanged += OnTutorialPageMaskingSettingsChanged;
            TutorialPage.TutorialPageNonMaskingSettingsChanged += OnTutorialPageNonMaskingSettingsChanged;
            EditorApplication.playModeStateChanged -= TrackPlayModeChanging;
            EditorApplication.playModeStateChanged += TrackPlayModeChanging;
        }

        VisualTreeAsset tutorialContentAssetCached;

        async void InitializeUI(bool fetchRoboLindsay = true)
        {


            m_WindowTitleContent = Tr("Tutorials");
            m_HomePromptTitle = Tr("Return to Tutorials?");
            m_HomePromptText = Tr(
                "Returning to the Tutorial Selection means exiting the tutorial and losing all of your progress\n" +
                "Do you wish to continue?"
            );
            m_PromptYes = Tr("Yes");
            m_PromptNo = Tr("No");
            m_PromptOk = Tr("OK");
            // Unity's menu guide convetion: text in italics, '>' used as a separator
            // NOTE EditorUtility.DisplayDialog doesn't support italics so cannot use rich text here.
            m_MenuPathGuide = Tr(TutorialWindowMenuItem.Menu) + " > " + Tr(TutorialWindowMenuItem.Item);

            m_TabClosedDialogTitle = Tr("Close Tutorials");
            m_TabClosedDialogText = string.Format(Tr("You can find Tutorials later by choosing {0} in the top menu."), m_MenuPathGuide);

            rootVisualElement.Clear();



            IMGUIContainer imguiToolBar = new IMGUIContainer(OnGuiToolbar);
            IMGUIContainer videoBox = new IMGUIContainer(RenderVideoIfPossible);
            videoBox.style.alignSelf = new StyleEnum<Align>(Align.Center);
            videoBox.name = "VideoBox";

            var root = rootVisualElement;
            var topBarAsset = LoadUIAsset<VisualTreeAsset>("Main.uxml");
            var tutorialContentAsset = LoadUIAsset<VisualTreeAsset>("TutorialContents.uxml");
            tutorialContentAssetCached = tutorialContentAsset;
            VisualElement tutorialImage = topBarAsset.CloneTree().Q("TutorialImage");
            VisualElement tutorialMenuCard = topBarAsset.CloneTree().Q("CardContainer");

            VisualElement tutorialContents = tutorialContentAsset.CloneTree().Q("TutorialEmptyContents");
            tutorialContents.style.flexGrow = 1f;
            VisualElement TutorialContentPage = tutorialContentAsset.CloneTree().Q("TutorialPageContainer");
            VisualElement TutorialTopBar = TutorialContentPage.Q("Header");

            VisualElement linkButton = topBarAsset.CloneTree().Q("LinkButton");


            //lfwells added tabs for KIT109
            VisualElement tabContainer = tutorialContents.Q("TutorialListTabs");
            CreateTabs(topBarAsset, tabContainer);

            var tabTitleText = tutorialContents.Q<Label>("TutorialListTabTitle");
            if (CurrentTab >= 0 && CurrentTab < TabTooltips.Count && !string.IsNullOrEmpty(TabTooltips[CurrentTab]))
            {
                tabTitleText.text = TabTooltips[CurrentTab];
            }
            else
            {
                tabTitleText.text = "";
            }
            var basePackageButtonText = tutorialContents.Q<Label>("TutorialListTabBasePackageInfo");
            if (TutorialWindow.FindReadme().showBasePackages == false) basePackageButtonText.RemoveFromHierarchy();
            var basePackageButton = tutorialContents.Q<Button>("TutorialListTabBasePackage");
            if (TutorialWindow.FindReadme().showBasePackages == false) basePackageButton.RemoveFromHierarchy();
            else
            {
                if (CurrentTab >= 0 && CurrentTab < TabBasePackages.Count && TabBasePackages[CurrentTab] != null)
                {
                    basePackageButton.clicked += () =>
                    {
                        if (EditorUtility.DisplayDialog("Download Base Package?", "Downloading the Base Package may overwrite some of your completed work. The tutorials are designed such that you can use your existing work to start the next tutorial. \n\nOnly use the base package if you are stuck and your version doesn't seem to work", "OK", "Cancel"))
                        {
                            var package = TabBasePackages[CurrentTab];

                            var url = Application.dataPath.Replace("Assets", "") + "/" + AssetDatabase.GetAssetPath(package);
                            url = url.Replace("C:/", "file:///c:/");

                            RoboAnalytics.RecordBasePackageDownload(package.name);

                            Application.OpenURL(url);
                        }
                    };
                }
                else
                {
                    basePackageButton.RemoveFromHierarchy();
                }
            }


            VisualElement cardContainer = topBarAsset.CloneTree().Q("TutorialListScrollView");
            cardContainer.style.alignItems = Align.Center;

            // TODO Don't create cards if we have tutorial in progress?
            CreateTutorialMenuCards(topBarAsset, cardContainer, CurrentTab); //[TODO] be careful: this will also trigger analytics event for link card impression

            tutorialContents.Add(cardContainer);
            VisualElement topBarVisElement = topBarAsset.CloneTree().Q("TitleHeader");
            //lfwells: added close popout and and export button
            topBarVisElement.Q<Button>("ExportButton").clicked += () =>
            {
                ExportTutorial.Export(GetProjectName());
            };
            topBarVisElement.Q<Button>("UndockButton").clicked += () =>
            {
                EditorWindowUtils.CenterOnMainWindow(GetOrCreateWindow());
            };
            topBarVisElement.Q("Close").RegisterCallback<MouseUpEvent>((e) =>
            {
                EditorUtility.DisplayDialog("Gone But Not Forgotten", "You can always show the tutorial window again by choosing Tutorials -> Show Tutorials.", "OK");
                GetOrCreateWindow().Close();
            });

            VisualElement footerBar = topBarAsset.CloneTree().Q("TutorialActions");

            TextElement titleElement = topBarVisElement.Q<TextElement>("TitleLabel");
            TextElement contextTextElement = topBarVisElement.Q<TextElement>("ContextLabel");

            UpdateHeader(contextTextElement, titleElement, topBarVisElement);

            root.Add(imguiToolBar);
            root.Add(TutorialTopBar);
            root.Add(videoBox);
            root.Add(topBarVisElement);



            //first do a check of the updates
            var systemUpdate = await UpdateManager.CheckTutorialSystemUpdateAvailable();
            if (systemUpdate)
            {
                var updateElement = topBarAsset.CloneTree().Q("UpdateMessage");
                updateElement.Q<Label>("Heading").text = "Tutorial System Update Available (Version " + (UpdateManager.LatestTutorialSystemVersion) + ")";
                updateElement.Q<Button>("Download").clicked += async () =>
                {
                    await UpdateManager.DownloadTutorialSystemUpdate();
                    InitializeUI();
                };
                root.Add(updateElement);
            }
            else
            {
                root.Q("UpdateMessage")?.RemoveFromHierarchy();

                var update = await UpdateManager.CheckTutorialUpdateAvailable();
                if (update)
                {
                    var updateElement = topBarAsset.CloneTree().Q("UpdateMessage");
                    updateElement.Q<Label>("Heading").text = "Tutorial Content Update Available (Version " + (UpdateManager.CurrentTutorialVersion + 1) + ")";
                    updateElement.Q<Button>("Download").clicked += async () =>
                    {
                        await UpdateManager.DownloadTutorialUpdate();
                        InitializeUI();
                    };
                    root.Add(updateElement);
                }
                else
                {
                    root.Q("UpdateMessage")?.RemoveFromHierarchy();
                    root.Q("SaveNameForm")?.RemoveFromHierarchy();
                    if (RoboAnalytics.HasRecordedStudentIDAndName() == false)
                    {
                        var nameElement = topBarAsset.CloneTree().Q("SaveNameForm");
                        nameElement.Q<TextField>("Username").value = System.Environment.UserName;
                        nameElement.Q<Button>("Save").clicked += () =>
                        {
                            RoboAnalytics.SaveNameAndID(nameElement.Q<TextField>("Username").value, nameElement.Q<TextField>("StudentID").value);
                            InitializeUI();
                        };
                        root.Add(nameElement);
                    }
                    else if (forceReshowUsernameID)
                    {
                        var nameElement = topBarAsset.CloneTree().Q("SaveNameForm");
                        nameElement.Q<TextField>("Username").value = RoboAnalytics.HasRecordedStudentIDAndName() ? RoboAnalytics.Username : System.Environment.UserName;
                        nameElement.Q<TextField>("StudentID").value = RoboAnalytics.HasRecordedStudentIDAndName() ? RoboAnalytics.StudentID : "";
                        nameElement.Q<Button>("Save").clicked += () =>
                        {
                            forceReshowUsernameID = false;
                            RoboAnalytics.SaveNameAndID(nameElement.Q<TextField>("Username").value, nameElement.Q<TextField>("StudentID").value);

                            InitializeUI();
                        };
                        root.Add(nameElement);
                    }

                }
            }


            root.Add(tutorialContents);

            Styles.ApplyThemeStyleSheetTo(root);

            VisualElement tutorialContainer = TutorialContentPage.Q("TutorialContainer");
            tutorialContainer.Add(linkButton);
            root.Add(tutorialContainer);

            footerBar.Q<Button>("PreviousButton").clicked += OnPreviousButtonClicked;
            footerBar.Q<Button>("NextButton").RegisterCallback<MouseUpEvent>(OnNextButtonClicked);//.clickable.clickedWithEventInfo += OnNextButtonClicked;

            // Set here in addition to CreateWindow() so that title of old saved layouts is overwritten,
            // also make sure the title is translated always.
            titleContent.text = m_WindowTitleContent;

            VideoPlaybackManager.OnEnable();

            GUIViewProxy.PositionChanged += OnGUIViewPositionChanged;
            HostViewProxy.actualViewChanged += OnHostViewActualViewChanged;
            Tutorial.TutorialPagesModified += OnTutorialPagesModified;

            root.Add(footerBar);
            SetUpTutorial();

            MaskingEnabled = true;

            readme = FindReadme();
            EditorCoroutineUtility.StartCoroutineOwnerless(DelayedOnEnable());

            //Debug.Log("fetchRoboLindsay"+fetchRoboLindsay);
            if (fetchRoboLindsay)
            {
                //finally, we load the user's progress
                var results = await RoboAnalytics.LoadTutorialSectionsCompleted();
                //Debug.Log(string.Join(",", results));

                foreach (var card in m_Cards)
                {
                    //Debug.Log("CHECK: "+ card.Section.TutorialId+" -- "+results.Contains(card.Section.TutorialId));
                    if (results.Contains(card.Section.TutorialId))
                    {
                        card.Section.SetTutorialCompletedWithoutServer();

                    }
                    UpdateCheckmark(card);
                }
                //await InitializeUI(fetchRoboLindsay: false);
            }
        }

        [MenuItem(TutorialWindowMenuItem.MenuPath + "Show ID and Username", priority = 2)]
        public static void ShowUsernameID()
        {
            forceReshowUsernameID = true;
            GetOrCreateWindow().InitializeUI();
        }

        void ExitClicked(MouseUpEvent mouseup)
        {
            SkipTutorial();
        }

        void SetIntroScreenVisible(bool visible)
        {
            if (visible)
            {
                ShowElement("TitleHeader");
                HideElement("TutorialActions");
                HideElement("Header");
                ShowElement("TutorialEmptyContents");
                // SHOW: tutorials
                // HIDE: tutorial steps
                HideElement("TutorialContainer");
                // Show card container
            }
            else
            {
                HideElement("TitleHeader");
                ShowElement("TutorialActions");
                VisualElement headerElement = rootVisualElement.Q("Header");
                ShowElement(headerElement);
                headerElement.Q<Label>("HeaderLabel").text = currentTutorial.TutorialTitle;
                headerElement.Q<Label>("StepCount").text = $"{currentTutorial.CurrentPageIndex + 1} / {currentTutorial.m_Pages.Count}";
                headerElement.Q("Close").RegisterCallback<MouseUpEvent>(ExitClicked);
                //HideElement("TutorialImage");
                HideElement("TutorialEmptyContents");
                ShowElement("TutorialContainer");
                //ShowElement("VideoBox");
                // Hide card container
            }
            if (rootVisualElement.Q<Button>("PreviousButton") != null)
            {
                rootVisualElement.Q<Button>("PreviousButton").text = m_BackButtonText;
                rootVisualElement.Q<Button>("NextButton").text = m_NextButtonText;
            }
        }

        void ShowElement(string name, VisualElement rootVisualElement = null) => ShowElement((rootVisualElement ?? this.rootVisualElement).Q(name));
        void HideElement(string name, VisualElement rootVisualElement = null) => HideElement((rootVisualElement ?? this.rootVisualElement).Q(name));

        static void ShowElement(VisualElement elem) => SetElementVisible(elem, true);
        static void HideElement(VisualElement elem) => SetElementVisible(elem, false);

        static void SetElementVisible(VisualElement elem, bool visible)
        {
            if (elem == null) { return; }
            elem.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        }

        void OnDisable()
        {
            if (!m_PlayModeChanging)
            {
                AnalyticsHelper.TutorialEnded(TutorialConclusion.Quit);
            }

            Criterion.CriterionCompleted -= OnCriterionCompleted;

            ClearTutorialListener();

            Tutorial.TutorialPagesModified -= OnTutorialPagesModified;
            TutorialPage.CriteriaCompletionStateTested -= OnTutorialPageCriteriaCompletionStateTested;
            TutorialPage.TutorialPageMaskingSettingsChanged -= OnTutorialPageMaskingSettingsChanged;
            TutorialPage.TutorialPageNonMaskingSettingsChanged -= OnTutorialPageNonMaskingSettingsChanged;
            GUIViewProxy.PositionChanged -= OnGUIViewPositionChanged;
            HostViewProxy.actualViewChanged -= OnHostViewActualViewChanged;

            VideoPlaybackManager.OnDisable();

            ApplyMaskingSettings(false);
        }

        void OnDestroy()
        {
            // TODO SkipTutorial();?

            // Play mode might trigger layout change (maximize on play) and closing of this window also.
            if (ShowTutorialsClosedDialog && !TutorialManager.IsLoadingLayout && !m_PlayModeChanging)
            {
                // Delay call prevents us getting the dialog upon assembly reload.
                EditorApplication.delayCall += delegate
                {
                    ShowTutorialsClosedDialog.SetValue(false);
                    EditorUtility.DisplayDialog(m_TabClosedDialogTitle, m_TabClosedDialogText, m_PromptOk);
                };
            }
        }

        void OnHostViewActualViewChanged()
        {
            if (TutorialManager.IsLoadingLayout) { return; }
            // do not mask immediately in case unmasked GUIView doesn't exist yet
            // TODO disabled for now in order to get Welcome dialog masking working
            //QueueMaskUpdate();
        }

        void QueueMaskUpdate()
        {
            EditorApplication.update -= ApplyQueuedMask;
            EditorApplication.update += ApplyQueuedMask;
        }

        void OnTutorialPageCriteriaCompletionStateTested(TutorialPage sender)
        {
            if (currentTutorial == null || currentTutorial.CurrentPage != sender) { return; }

            if (sender.AreAllCriteriaSatisfied && sender.AutoAdvanceOnComplete && !sender.HasMovedToNextPage)
            {
                EditorCoroutineUtility.StartCoroutineOwnerless(GoToNextPageAfterDelay());
                return;
            }

            ApplyMaskingSettings(true);
        }

        IEnumerator GoToNextPageAfterDelay()
        {
            yield return new EditorWaitForSeconds(0.5f);

            if (currentTutorial != null && currentTutorial.TryGoToNextPage())
            {
                UpdatePageState();
                yield break;
            }
            ApplyMaskingSettings(true);
        }

        void SkipTutorial()
        {
            if (currentTutorial == null) { return; }

            switch (currentTutorial.SkipTutorialBehavior)
            {
                case Tutorial.SkipTutorialBehaviorType.SameAsExitBehavior: ExitTutorial(false); break;
                case Tutorial.SkipTutorialBehaviorType.SkipToLastPage: currentTutorial.SkipToLastPage(); break;
                default: throw new ArgumentOutOfRangeException();
            }
        }

        IEnumerator ExitTutorialAndPlaymode(bool completed)
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.isPlaying = false;
                while (EditorApplication.isPlaying)
                {
                    yield return null;
                }
            }
            else
            {
                yield return null;
            }
            ExitTutorial(completed);
        }

        void ExitTutorial(bool completed)
        {
            if (EditorApplication.isPlaying)
            {
                /* todo: this requires a frame anyway, so the save dialog won't show
                 * if we want to support the save dialog even in that case, then we should use "ExitTutorialAndPlaymode" coroutine 
                 * instead of directly calling this method.
                 * However, using that coroutine breaks the tutorial switching system due to race conditions.
                 * I'm leaving both that routine and this comment here so we know what to do in the future.
                 */
                EditorApplication.isPlaying = false;
            }

            if (!TutorialManager.instance.IsTransitioningBetweenTutorials
                && !EditorApplication.isPlaying && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            // New behaviour: exiting resets and nullifies the current tutorial and shows the project's tutorials.
            if (completed)
            {
                SetTutorial(null, false);
                ResetTutorial();
                TutorialManager.instance.RestoreOriginalState();
            }
            else
            {
                currentTutorial.CurrentPage.RaiseOnBeforeQuitTutorialEvent();
                SetTutorial(null, false);
                ResetTutorial();
                TutorialManager.instance.RestoreOriginalState();
            }

            //todo: Note to Ali/Mika: can we remove the code below, as it is unused?
            // TODO new behaviour testing: assetSelectedOnExit was originally used for selecting
            // Readme but this is not required anymore as the TutorialWindow contains Readme's functionality.
            //if (currentTutorial?.assetSelectedOnExit != null)
            //    Selection.activeObject = currentTutorial.assetSelectedOnExit;

            //SaveTutorialStates();
        }

        void OnTutorialInitiated()
        {
            if (!currentTutorial) { return; }

            AnalyticsHelper.TutorialStarted(currentTutorial);
            GenesisHelper.LogTutorialStarted(currentTutorial.LessonId);
            CreateTutorialViews();
        }

        void OnTutorialCompleted(bool exitTutorial)
        {
            if (!currentTutorial) { return; }

            AnalyticsHelper.TutorialEnded(TutorialConclusion.Completed);
            GenesisHelper.LogTutorialEnded(currentTutorial.LessonId);
            MarkTutorialCompleted(currentTutorial.LessonId, currentTutorial.Completed);

            if (!exitTutorial) { return; }
            ExitTutorial(currentTutorial.Completed);
        }

        internal void CreateTutorialViews()
        {
            m_AllParagraphs = currentTutorial.Pages.SelectMany(pg => pg.Paragraphs).ToList();
        }

        List<TutorialParagraph> GetCurrentParagraph()
        {
            if (m_Indexes == null || m_Indexes.Length != currentTutorial.PageCount)
            {
                // Update page to paragraph index
                m_Indexes = new int[currentTutorial.PageCount];
                var pageIndex = 0;
                var paragraphIndex = 0;
                foreach (var page in currentTutorial.Pages)
                {
                    m_Indexes[pageIndex++] = paragraphIndex;
                    if (page != null)
                        paragraphIndex += page.Paragraphs.Count();
                }
            }

            List<TutorialParagraph> tmp = new List<TutorialParagraph>();
            if (m_Indexes.Length > 0)
            {
                var endIndex = currentTutorial.CurrentPageIndex + 1 > currentTutorial.PageCount - 1
                    ? m_AllParagraphs.Count
                    : m_Indexes[currentTutorial.CurrentPageIndex + 1];
                for (int i = m_Indexes[currentTutorial.CurrentPageIndex]; i < endIndex; i++)
                {
                    tmp.Add(m_AllParagraphs[i]);
                }
            }
            return tmp;
        }

        // TODO 'page' and 'index' unused
        internal void PrepareNewPage(TutorialPage page = null, int index = 0)
        {
            if (currentTutorial == null) return;
            if (!m_AllParagraphs.Any())
            {
                CreateTutorialViews();
            }
            m_Paragraphs.Clear();

            if (currentTutorial.CurrentPage == null)
            {
                m_NextButtonText = string.Empty;
            }
            else
            {
                m_NextButtonText = IsLastPage()
                    ? currentTutorial.CurrentPage.DoneButton
                    : currentTutorial.CurrentPage.NextButton;
            }
            m_BackButtonText = IsFirstPage() ? Tr("All Tutorials") : Tr("Back");

            m_Paragraphs = GetCurrentParagraph();

            m_Paragraphs.TrimExcess();

            EditorCoroutineUtility.StartCoroutineOwnerless(DelayedShowCurrentTutorialContent());
        }

        IEnumerator DelayedShowCurrentTutorialContent()
        {
            while (!m_IsInitialized)
            {
                yield return null;
            }
            ShowCurrentTutorialContent(); // HACK
        }

        internal void ForceInititalizeTutorialAndPage()
        {
            m_FarthestPageCompleted = -1;

            CreateTutorialViews();
            PrepareNewPage();
        }

        static void OpenLoadTutorialDialog()
        {
            string assetPath = EditorUtility.OpenFilePanel("Load a Tutorial", "Assets", "asset");
            if (string.IsNullOrEmpty(assetPath)) { return; }
            assetPath = string.Format("Assets{0}", assetPath.Substring(Application.dataPath.Length));
            TutorialManager.instance.StartTutorial(AssetDatabase.LoadAssetAtPath<Tutorial>(assetPath));
            GUIUtility.ExitGUI();
        }

        bool IsLastPage() { return currentTutorial != null && currentTutorial.PageCount - 1 <= currentTutorial.CurrentPageIndex; }

        bool IsFirstPage() { return currentTutorial != null && currentTutorial.CurrentPageIndex == 0; }

        // Returns true if some real progress has been done (criteria on some page finished).
        bool IsInProgress()
        {
            return currentTutorial
                ?.Pages.Any(pg => pg.Paragraphs.Any(p => p.Criteria.Any() && pg.AreAllCriteriaSatisfied))
                ?? false;
        }

        void ClearTutorialListener()
        {
            if (currentTutorial == null) { return; }

            currentTutorial.TutorialInitiated -= OnTutorialInitiated;
            currentTutorial.TutorialCompleted -= OnTutorialCompleted;
            currentTutorial.PageInitiated -= OnShowPage;
            currentTutorial.StopTutorial();
        }

        internal void SetTutorial(Tutorial tutorial, bool resetProgress)
        {
            ClearTutorialListener();

            ResetTutorialProgress(currentTutorial, resetProgress);
            currentTutorial = tutorial;
            ResetTutorialProgress(currentTutorial, resetProgress);

            ApplyMaskingSettings(currentTutorial != null);
            SetUpTutorial();
        }

        void ResetTutorialProgress(Tutorial tutorial, bool resetProgress)
        {
            if (tutorial == null) { return; }
            if (resetProgress)
            {
                tutorial.ResetProgress();
            }
            m_AllParagraphs.Clear();
            m_Paragraphs.Clear();
        }

        void SetUpTutorial()
        {
            // bail out if this instance no longer exists such as when e.g., loading a new window layout
            if (this == null || currentTutorial == null || currentTutorial.CurrentPage == null) { return; }

            if (currentTutorial.CurrentPage != null)
            {
                currentTutorial.CurrentPage.Initiate();
            }

            currentTutorial.TutorialInitiated += OnTutorialInitiated;
            currentTutorial.TutorialCompleted += OnTutorialCompleted;
            currentTutorial.PageInitiated += OnShowPage;

            if (m_AllParagraphs.Any())
            {
                PrepareNewPage();
                return;
            }
            ForceInititalizeTutorialAndPage();
        }

        void ApplyQueuedMask()
        {
            if (IsParentNull()) { return; }

            EditorApplication.update -= ApplyQueuedMask;
            ApplyMaskingSettings(true);
        }

        IEnumerator DelayedOnEnable()
        {
            yield return null;

            do
            {
                yield return null;
                m_VideoBoxElement = rootVisualElement.Q("TutorialMediaContainer");
            }
            while (m_VideoBoxElement == null);


            if (currentTutorial == null)
            {
                if (m_VideoBoxElement != null)
                {
                    HideElement(m_VideoBoxElement);
                }
            }
            VideoPlaybackManager.OnEnable();
        }

        void OnGuiToolbar()
        {
            // TODO calling SetIntroScreenVisible every OnGUI, not probably wanted.
            SetIntroScreenVisible(currentTutorial == null);
            if (s_AuthoringMode)
                ToolbarGUI();
        }

        void OnPreviousButtonClicked()
        {
            if (IsFirstPage())
            {
                SkipTutorial();
            }
            else
            {
                currentTutorial.GoToPreviousPage();
                UpdatePageState();
                // TODO OnNextButtonClicked has ShowCurrentTutorialContent() but this doesn't --
                // is this on purpose?
            }
        }

        void OnNextButtonClicked(MouseUpEvent evt)
        {
            bool forceIt = false;
            if (nextButtonDisabled)
            {
                if (evt.shiftKey)//if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    if (EditorUtility.DisplayDialog("Skip Page", "Skipping a page is not recommendend, and is something you should only consider if completely stuck.\n\nLikely not completing the steps on this page properly may result in bugs later on.", "Skip Page", "Cancel"))
                    {
                        forceIt = true;
                        RoboAnalytics.RecordPageSkip(currentTutorial.name, currentTutorial.CurrentPage.name);
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }

            if (currentTutorial)
                currentTutorial.TryGoToNextPage(forceIt);

            UpdatePageState();
            ShowCurrentTutorialContent();
        }

        // Resets the contents of this window. Use this before saving layouts for tutorials.
        internal void Reset()
        {
            m_AllParagraphs.Clear();
            SetTutorial(null, true);
            readme = null;
        }

        void ToolbarGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar, GUILayout.ExpandWidth(true));

            bool Button(string text)
            {
                return GUILayout.Button(text, EditorStyles.toolbarButton, GUILayout.MaxWidth(s_AuthoringModeToolbarButtonWidth));
            }

            using (new EditorGUI.DisabledScope(currentTutorial == null))
            {
                if (Button(Tr("Select Tutorial")))
                {
                    Selection.activeObject = currentTutorial;
                }

                using (new EditorGUI.DisabledScope(currentTutorial?.CurrentPage == null))
                {
                    if (Button(Tr("Select Page")))
                    {
                        Selection.activeObject = currentTutorial.CurrentPage;
                    }
                }

                if (Button(Tr("Skip To End")))
                {
                    currentTutorial.SkipToLastPage();
                }
            }

            GUILayout.FlexibleSpace();

            using (new EditorGUI.DisabledScope(currentTutorial == null))
            {
                EditorGUI.BeginChangeCheck();
                MaskingEnabled = GUILayout.Toggle(
                    MaskingEnabled, Tr("Preview Masking"), EditorStyles.toolbarButton,
                    GUILayout.MaxWidth(s_AuthoringModeToolbarButtonWidth)
                );
                if (EditorGUI.EndChangeCheck())
                {
                    ApplyMaskingSettings(true);
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            if (Button(Tr("Run Startup Code")))
            {
                UserStartupCode.RunStartupCode();
            }

            EditorGUILayout.EndHorizontal();
        }

        void OnTutorialPagesModified(Tutorial sender)
        {
            if (currentTutorial == null || currentTutorial != sender) { return; }

            CreateTutorialViews();
            ShowCurrentTutorialContent();

            ApplyMaskingSettings(true);
        }

        void OnTutorialPageMaskingSettingsChanged(TutorialPage sender)
        {
            if (currentTutorial == null || currentTutorial.CurrentPage != sender) { return; }

            ApplyMaskingSettings(true);
        }

        void OnTutorialPageNonMaskingSettingsChanged(TutorialPage sender)
        {
            if (currentTutorial == null || currentTutorial.CurrentPage != sender) { return; }

            ShowCurrentTutorialContent();
        }

        void OnShowPage(TutorialPage page, int index)
        {
            page.RaiseOnBeforePageShownEvent();
            m_FarthestPageCompleted = Mathf.Max(m_FarthestPageCompleted, index - 1);
            ApplyMaskingSettings(true);

            AnalyticsHelper.PageShown(page, index);
            PrepareNewPage();

            VideoPlaybackManager.ClearCache();
            page.RaiseOnAfterPageShownEvent();
        }

        void OnGUIViewPositionChanged(UnityObject sender)
        {
            if (TutorialManager.IsLoadingLayout || sender.GetType().Name == "TooltipView") { return; }

            ApplyMaskingSettings(true);
        }

        void ApplyMaskingSettings(bool applyMask)
        {
            //            Debug.Log("ApplyMaskingSettings called with " + applyMask);
            // TODO IsParentNull() probably not needed anymore as TutorialWindow is always parented in the current design & layout.
            if (!applyMask || !MaskingEnabled || currentTutorial == null
                || currentTutorial.CurrentPage == null || IsParentNull() || TutorialManager.IsLoadingLayout)
            {
                MaskingManager.Unmask();
                InternalEditorUtility.RepaintAllViews();
                //                Debug.LogWarning("Bailed early");
                return;
            }

            MaskingSettings maskingSettings = currentTutorial.CurrentPage.CurrentMaskingSettings;
            try
            {
                if (maskingSettings == null || !maskingSettings.Enabled)
                {
                    //                    Debug.LogWarning("Unmask");
                    MaskingManager.Unmask();
                }
                else
                {
                    bool foundAncestorProperty;
                    var unmaskedViews = UnmaskedView.GetViewsAndRects(maskingSettings.UnmaskedViews, out foundAncestorProperty);
                    if (foundAncestorProperty)
                    {
                        // Keep updating mask when target property is not unfolded
                        QueueMaskUpdate();
                    }

                    if (currentTutorial.CurrentPageIndex <= m_FarthestPageCompleted)
                    {
                        unmaskedViews = new UnmaskedView.MaskData();
                    }

                    UnmaskedView.MaskData highlightedViews;

                    if (unmaskedViews.Count > 0) //Unmasked views should be highlighted
                    {
                        //                        Debug.LogWarning("Count > 0");

                        highlightedViews = (UnmaskedView.MaskData)unmaskedViews.Clone();
                    }
                    //else if (CanMoveToNextPage) // otherwise, if the current page is completed, highlight this window //removed by Ian, I have no idea why "highlight /this/ window" is a good idea, 'cos this is what causes random incorrect inspector highlights
                    //{
                    //    Debug.LogWarning("CanMoveToNextPage");

                    //    highlightedViews = new UnmaskedView.MaskData();
                    //    highlightedViews.AddParentFullyUnmasked(this);
                    //}
                    else // otherwise, highlight manually specified control rects if there are any
                    {
                        //                        Debug.LogWarning("Otherwise");

                        var unmaskedControls = new List<GuiControlSelector>();
                        var unmaskedViewsWithControlsSpecified =
                            maskingSettings.UnmaskedViews.Where(v => v.GetUnmaskedControls(unmaskedControls) > 0).ToArray();
                        // if there are no manually specified control rects, highlight all unmasked views
                        highlightedViews = UnmaskedView.GetViewsAndRects(
                            unmaskedViewsWithControlsSpecified.Length == 0 ?
                            maskingSettings.UnmaskedViews : unmaskedViewsWithControlsSpecified
                        );
                    }

                    // ensure tutorial window's HostView and tooltips are not masked
                    unmaskedViews.AddParentFullyUnmasked(this);
                    unmaskedViews.AddTooltipViews();

                    // tooltip views should not be highlighted
                    highlightedViews.RemoveTooltipViews();

                    MaskingManager.Mask(
                        unmaskedViews,
                        Styles == null ? Color.magenta * new Color(1f, 1f, 1f, 0.8f) : Styles.MaskingColor,
                        highlightedViews,
                        Styles == null ? Color.cyan * new Color(1f, 1f, 1f, 0.8f) : Styles.HighlightColor,
                        Styles == null ? new Color(1, 1, 1, 0.5f) : Styles.BlockedInteractionColor,
                        Styles == null ? 3f : Styles.HighlightThickness
                    );
                }
            }
            catch (ArgumentException e)
            {
                if (s_AuthoringMode)
                    Debug.LogException(e, currentTutorial.CurrentPage);
                else
                    Console.WriteLine(StackTraceUtility.ExtractStringFromException(e));

                MaskingManager.Unmask();
            }
            finally
            {
                InternalEditorUtility.RepaintAllViews();
            }
        }

        void ResetTutorialOnDelegate(PlayModeStateChange playmodeChange)
        {
            switch (playmodeChange)
            {
                case PlayModeStateChange.EnteredEditMode:
                    EditorApplication.playModeStateChanged -= ResetTutorialOnDelegate;
                    ResetTutorial();
                    break;
            }
        }

        internal void ResetTutorial()
        {
            if (EditorApplication.isPlaying)
            {
                EditorApplication.playModeStateChanged += ResetTutorialOnDelegate;
                EditorApplication.isPlaying = false;
                return;
            }
            else if (!EditorApplication.isPlaying)
            {
                m_FarthestPageCompleted = -1;
                TutorialManager.instance.ResetTutorial();
            }
        }

        /// <summary>
        /// Returns Readme iff one Readme exists in the project.
        /// TODO make internal in 2.0
        /// </summary>
        /// <returns></returns>
        public static TutorialContainer FindReadme()
        {
            var ids = AssetDatabase.FindAssets($"t:{typeof(TutorialContainer).FullName}");
            return ids.Length == 1
                ? (TutorialContainer)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids[0]))
                : null;
        }

        float checkLanguageTick = 0f;
        float blinkTick = 0f;
        bool blinkOn = true;

        float editorDeltaTime = 0f;
        float lastTimeSinceStartup = 0f;


        private void SetEditorDeltaTime()
        {
            if (lastTimeSinceStartup == 0f)
            {
                lastTimeSinceStartup = (float)EditorApplication.timeSinceStartup;
            }
            editorDeltaTime = (float)EditorApplication.timeSinceStartup - lastTimeSinceStartup;
            lastTimeSinceStartup = (float)EditorApplication.timeSinceStartup;
        }

        void Update()
        {
            SetEditorDeltaTime();

            blinkTick += editorDeltaTime;
            checkLanguageTick += editorDeltaTime;
            currentTutorial?.CurrentPage?.RaiseOnTutorialPageStayEvent();

            if (blinkTick > 1f)
            {
                blinkTick -= 1f;
                if (IsFirstPage())
                {
                    if (blinkOn)
                    {
                        ShowElement("NextButtonBase");
                    }
                    else
                    {
                        HideElement("NextButtonBase");
                    }
                    blinkOn = !blinkOn;
                }
            }

            if (checkLanguageTick >= 1f)
            {
                checkLanguageTick = 0f;
                if (LocalizationDatabaseProxy.currentEditorLanguage != m_CurrentEditorLanguage)
                {
                    m_CurrentEditorLanguage = LocalizationDatabaseProxy.currentEditorLanguage;
                    InitializeUI();
                }
            }
        }

        internal void MarkAllTutorialsUncompleted()
        {
            Sections.ToList().ForEach(s => MarkTutorialCompleted(s.TutorialId, false));
            foreach (var card in m_Cards)
                UpdateCheckmark(card);
        }

        void LoadTutorialStates()
        {
            Sections.ToList().ForEach(s => s.LoadState());
        }

        // Fetches statuses from the web API
        internal void FetchTutorialStates()
        {
            m_DoneFetchingTutorialStates = false;
            GenesisHelper.GetAllTutorials((tutorials) =>
            {
                tutorials.ForEach(t => MarkTutorialCompleted(t.lessonId, t.status == "Finished"));
                m_DoneFetchingTutorialStates = true;
            });
        }

        void MarkTutorialCompleted(string lessonId, bool completed)
        {
            foreach (var section in Array.FindAll(Sections, s => s.TutorialId == lessonId))
            {
                if (section.TutorialCompleted) continue;
                section.TutorialCompleted = completed;
                section.SaveState();
            }
        }

        //https://answers.unity.com/questions/16804/retrieving-project-name.html
        public string GetProjectName()
        {
            string[] s = Application.dataPath.Split('/');
            string projectName = s[s.Length - 2];
            return projectName;
        }
    }
}