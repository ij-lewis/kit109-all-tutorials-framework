using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Xml.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;

namespace Unity.Tutorials.Core.Editor
{
    /// <summary>
    /// Creates UIToolkit elements from a rich text.
    /// </summary>
    public static class RichTextParser
    {
        // Tries to parse text to XDocument word by word - outputs the longest successful string before failing
        static string ShowContentWithError(string errorContent)
        {
            string longestString = "";
            string previousLongestString = "";
            string[] lines = errorContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            foreach (string line in lines)
            {
                string[] words = line.Split(new[] { " " }, StringSplitOptions.None);
                foreach (string word in words)
                {
                    longestString += word + " ";
                    try
                    {
                        XDocument.Parse("<content>" + longestString + "</content>");
                    }
                    catch
                    {
                        continue;
                    }
                    previousLongestString = longestString;
                }
                longestString += "\r\n";
            }
            return previousLongestString;
        }

        /// <summary>
        /// Preprocess rich text - add space around tags.
        /// </summary>
        /// <param name="inputText">Text with tags</param>
        /// <returns>Text with space around tags</returns>
        static string PreProcessRichText(string inputText)
        {
            string processed = inputText;
            processed = processed.Replace("<b>", " <b>");
            processed = processed.Replace("<i>", " <i>");
            processed = processed.Replace("<code>", " <code>");
            processed = processed.Replace("<inlinecode>", " <inlinecode>");
            processed = processed.Replace("<c>", " <c>");
            processed = processed.Replace("<asset>", " <asset>");
            processed = processed.Replace("<gameobject>", " <gameobject>");
            processed = processed.Replace("<go>", " <go>");
            processed = processed.Replace("<a ", " <a ");
            processed = processed.Replace("<a>", "  <a>");
            processed = processed.Replace("<br", " <br");
            processed = processed.Replace("<wordwrap>", " <wordwrap>");
            processed = processed.Replace("<doc>", " <doc>");

            processed = processed.Replace("</b>", "</b> ");
            processed = processed.Replace("</i>", "</i> ");
            processed = processed.Replace("</code>", "</code> ");
            processed = processed.Replace("</inlinecode>", "</inlinecode> ");
            processed = processed.Replace("</c>", "</c> ");
            processed = processed.Replace("</asset>", "</asset> ");
            processed = processed.Replace("</gameobject>", "</gameobject> ");
            processed = processed.Replace("</go>", "</go> ");
            processed = processed.Replace("</a>", "</a> ");
            processed = processed.Replace("</wordwrap>", " </wordwrap> ");
            processed = processed.Replace("</doc>", "</doc>");

            return processed;
        }

        /// <summary>
        /// Helper function to detect if the string contains any characters in
        /// the Unicode range reserved for Chinese, Japanese and Korean characters.
        /// </summary>
        /// <param name="textLine">String to check for CJK letters.</param>
        /// <returns>True if it contains Chinese, Japanese or Korean characters.</returns>
        static bool NeedSymbolWrapping(string textLine)
        {
            // Unicode range for CJK letters.
            // Range chosen from StackOverflow: https://stackoverflow.com/a/42411925
            // Validated from sources:
            // https://www.unicode.org/faq/han_cjk.html
            // https://en.wikipedia.org/wiki/CJK_Unified_Ideographs_(Unicode_block)
            return textLine.Any(c => (uint)c >= 0x4E00 && (uint)c <= 0x2FA1F);
        }

        /// <summary>
        /// Adds a new wrapping word Label to the target visualElement. Type can be BoldLabel, ItalicLabel or Label
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="textToAdd">The text inside the word label.</param>
        /// <param name="elementList">Redundant storage, mostly used for automated testing.</param>
        /// <param name="addToVisualElement">Parent container for the word Label.</param>
        /// <returns>The created label element.</returns>
        static VisualElement AddLabel<T>(string textToAdd, List<VisualElement> elementList, VisualElement addToVisualElement)
            where T : VisualElement
        {
            VisualElement wordLabel = null;
            Type LabelType = typeof(T);
            if (LabelType == typeof(ItalicLabel))
            {
                wordLabel = new ItalicLabel(textToAdd);
            }
            else if (LabelType == typeof(CodeLabel))
            {
                wordLabel = new CodeLabel(textToAdd);
            }
            else if (LabelType == typeof(InlineCodeLabel))
            {
                wordLabel = new InlineCodeLabel(textToAdd);
            }
            else if (LabelType == typeof(BoldLabel))
            {
                wordLabel = new BoldLabel(textToAdd);
            }
            else if (LabelType == typeof(TextLabel))
            {
                wordLabel = new TextLabel(textToAdd);
            }
            if (wordLabel == null)
            {
                Debug.LogError("Error: Unsupported Label type used. Use TextLabel, BoldLabel or ItalicLabel.");
                return null;
            }
            elementList.Add(wordLabel);
            addToVisualElement.Add(wordLabel);
            return wordLabel;
        }

        /// <summary>
        /// Transforms HTML tags to word element labels with different styles to enable rich text.
        /// </summary>
        /// <param name="htmlText"></param>
        /// <param name="targetContainer">
        /// The following need to set for the container's style:
        /// flex-direction: row;
        /// flex-wrap: wrap;
        /// </param>
        /// <returns>List of VisualElements made from the parsed text.</returns>
        /// 
        //TODO: Lindsay, this is where rich text parsing happens (poorly)
        public static List<VisualElement> RichTextToVisualElements(string htmlText, VisualElement targetContainer)
        {
            bool addError = false;
            string errorText = "";
            
            try
            {
                htmlText = htmlText.Replace("\\<", "&lt;").Replace("\\>", "&gt;");
                XDocument.Parse("<content>" + htmlText + "</content>");
            }
            catch (Exception e)
            {
                targetContainer.Clear();
                errorText = e.Message;
                htmlText = ShowContentWithError(htmlText);
                addError = true;
            }
            List<VisualElement> elements = new List<VisualElement>();

            targetContainer.Clear();
            bool boldOn = false; // <b> sets this on </b> sets off
            bool italicOn = false; // <i> </i>
            bool codeOn = false; // <code> </code>
            bool inlineCodeOn = false; // <inlinecode> </inlinecode>
            bool assetOn = false; //<asset>
            bool gameObjectOn = false; // <go>
            bool docOn = false; //<doc>
            bool forceWordWrap = false;
            bool linkOn = false;
            string linkURL = "";
            bool firstLine = true;
            bool lastLineHadText = false;
            bool previousElementWasAlsoInlineCode = false;
            // start streaming text per word to elements while retaining current style for each word block
            string[] lines = htmlText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            var currentCodeBlock = "";
            foreach (string lineTmp in lines)
            {   
                var line = lineTmp.Replace("&lt;", "<").Replace("&gt;", ">");
                if (codeOn)
                    line = line.Replace("\t", "   ");

                //Debug.Log("line '" + line + "'");


                // Check if the line begins with whitespace and turn that into corresponding Label
                string initialWhiteSpaces = "";
                foreach(char singleCharacter in line)
                {
                    if (singleCharacter == ' ' || singleCharacter == '\t' )
                    {
                        initialWhiteSpaces += singleCharacter;
                    }
                    else
                    {
                        break;
                    }
                }

                string processedLine = PreProcessRichText(line);
                if (line.Contains("<code>")) codeOn = true;

                // Separate the line into words
                string[] words = processedLine.Split(new[] { " " }, StringSplitOptions.None);

                if (!lastLineHadText && !codeOn)
                {
                    if (!firstLine)
                        elements.Add(AddParagraphToElement(targetContainer));

                    if (initialWhiteSpaces.Length > 0)
                    {
                        WhiteSpaceLabel indentationLabel = new WhiteSpaceLabel(initialWhiteSpaces);
                        targetContainer.Add(indentationLabel);
                        elements.Add(indentationLabel);
                    }
                }

                if (!firstLine && lastLineHadText && !codeOn)
                {
                    elements.Add(AddLinebreakToElement(targetContainer));
                    //Debug.Log("added a line break?");
                    lastLineHadText = false;
                    if (initialWhiteSpaces.Length > 0)
                    {
                        WhiteSpaceLabel indentationLabel = new WhiteSpaceLabel(initialWhiteSpaces);
                        targetContainer.Add(indentationLabel);
                        elements.Add(indentationLabel);
                    }
                }

                bool previousElementWasBold = false;
                for (var i = 0; i < words.Length; i++)
                {
                    var word = words[i];
                    var nextWord = i < words.Length-1 ? words[i + 1] : string.Empty;
                    // Wrap every character instead of word in case of Chinese and Japanese
                    // Note: override with <wordwrap>Force word wrapping here</wordwrap>

                    lastLineHadText = true;
                    string strippedWord = word;
                    bool removeBold = false;
                    bool removeItalic = false;
                    bool removeCode = false;
                    bool removeInlineCode = false;
                    bool removeAsset = false;
                    bool removeGameObject = false;
                    bool removeDoc = false;
                    bool addParagraph = false;
                    bool removeLink = false;
                    bool removeWordWrap = false;

                    var preTrim = strippedWord;
                    if (codeOn && preTrim == "") preTrim = " ";
                    strippedWord = strippedWord.Trim();

                    if (strippedWord.Contains("<b>"))
                    {
                        strippedWord = strippedWord.Replace("<b>", "");
                        boldOn = true;
                    }
                    if (strippedWord.Contains("<i>"))
                    {
                        strippedWord = strippedWord.Replace("<i>", "");
                        italicOn = true;
                    }
                    if (strippedWord.Contains("<code>"))
                    {
                        strippedWord = preTrim.Replace("<code>", "");
                        preTrim = preTrim.Replace("<code>", "");
                        codeOn = true;
                        currentCodeBlock = "";
                    }
                    if (strippedWord.Contains("<inlinecode>"))
                    {
                        strippedWord = strippedWord.Replace("<inlinecode>", "");
                        inlineCodeOn = true;
                    }
                    if (strippedWord.Contains("<c>"))
                    {
                        strippedWord = strippedWord.Replace("<c>", "");
                        inlineCodeOn = true;
                    }
                    if (strippedWord.Contains("<wordwrap>"))
                    {
                        strippedWord = strippedWord.Replace("<wordwrap>", "");
                        forceWordWrap = true;
                    }
                    if (strippedWord.Contains("<gameobject>"))
                    {
                        strippedWord = strippedWord.Replace("<gameobject>", "");
                        gameObjectOn = true;
                    }
                    if (strippedWord.Contains("<go>"))
                    {
                        strippedWord = strippedWord.Replace("<go>", "");
                        gameObjectOn = true;
                    }
                    if (strippedWord.Contains("<asset>"))
                    {
                        strippedWord = strippedWord.Replace("<asset>", "");
                        assetOn = true;
                    }
                    else if (strippedWord.Contains("<a>"))
                    {
                        strippedWord = strippedWord.Replace("<a>", "");
                        linkOn = true;
                    }
                    else if (strippedWord.Contains("<a"))
                    {
                        strippedWord = strippedWord.Replace("<a", "");
                        linkOn = true;
                    }
                    if (strippedWord.Contains("<doc>"))
                    {
                        strippedWord = strippedWord.Replace("<doc>", "");
                        docOn = true;
                    }


                    if (!codeOn)
                    {
                        if (word == "" || word == " " || word == "   ")  continue; 
                    }


                    bool wrapCharacters = !forceWordWrap && NeedSymbolWrapping(word);
                    if (linkOn && strippedWord.Contains("href="))
                    {
                        strippedWord = strippedWord.Replace("href=", "");
                        int linkFrom = strippedWord.IndexOf("\"", StringComparison.Ordinal) + 1;
                        int linkTo = strippedWord.LastIndexOf("\"", StringComparison.Ordinal);
                        linkURL = strippedWord.Substring(linkFrom, linkTo - linkFrom);
                        strippedWord = strippedWord.Substring(linkTo + 2, (strippedWord.Length - 2) - linkTo);
                        strippedWord.Replace("\">", "");
                    }
                    else if (linkOn)
                    {
                        strippedWord.Replace("\">", "");
                        linkURL = strippedWord.Replace("</a>" ,"");
                    }

                    if (strippedWord.Contains("</a>"))
                    {   
                        strippedWord = strippedWord.Replace("</a>", "");
                        removeLink = true;
                    }
                    if (strippedWord.Contains("<br/>"))
                    {
                        strippedWord = strippedWord.Replace("<br/>", "");
                        addParagraph = true;
                    }
                    if (strippedWord.Contains("</b>"))
                    {
                        strippedWord = strippedWord.Replace("</b>", "");
                        removeBold = true;
                    }
                    if (strippedWord.Contains("</i>"))
                    {
                        strippedWord = strippedWord.Replace("</i>", "");
                        removeItalic = true;
                    }
                    if (strippedWord.Contains("</code>"))
                    {
                        strippedWord = preTrim.Replace("</code>", "");
                        preTrim = preTrim.Replace("</code>", "");
                        
                        removeCode = true;
                    }
                    if (strippedWord.Contains("</inlinecode>"))
                    {
                        strippedWord = strippedWord.Replace("</inlinecode>", "");
                        removeInlineCode = true;
                    }
                    if (strippedWord.Contains("</c>"))
                    {
                        strippedWord = strippedWord.Replace("</c>", "");
                        removeInlineCode = true;
                    }
                    if (strippedWord.Contains("</asset>"))
                    {
                        strippedWord = strippedWord.Replace("</asset>", "");
                        removeAsset = true;
                    }
                    if (strippedWord.Contains("</gameobject>"))
                    {
                        strippedWord = strippedWord.Replace("</gameobject>", "");
                        removeGameObject = true;
                    }
                    if (strippedWord.Contains("</go>"))
                    {
                        strippedWord = strippedWord.Replace("</go>", "");
                        removeGameObject = true;
                    }
                    if (strippedWord.Contains("</doc>"))
                    {
                        strippedWord = strippedWord.Replace("</doc>", "");
                        removeDoc = true;
                    }
                    if (strippedWord.Contains("</wordwrap>"))
                    {
                        strippedWord = strippedWord.Replace("</wordwrap>", "");
                        removeWordWrap = true;
                    }
                    if (boldOn && strippedWord != "")
                    {
                        if (wrapCharacters)
                        {
                            foreach(char character in strippedWord)
                            {
                                AddLabel<BoldLabel>(character.ToString(), elements, targetContainer);
                            }
                        }
                        else
                        {
                            var boldLabel = AddLabel<BoldLabel>(strippedWord, elements, targetContainer);
                            //if the next word is a full stop, then no padding on the right
                            if (nextWord == ".")
                            {
                                boldLabel.style.paddingRight = 0;
                                previousElementWasBold = true;
                            }
                        }
                    }
                    else if (italicOn && strippedWord != "")
                    {
                        if (wrapCharacters)
                        {
                            foreach(char character in strippedWord)
                            {
                                AddLabel<ItalicLabel>(character.ToString(), elements, targetContainer);
                            }
                        }
                        else
                        {
                            AddLabel<ItalicLabel>(strippedWord, elements, targetContainer);
                        }
                    }
                    else if (codeOn && preTrim != "")
                    {
                        currentCodeBlock+=preTrim;
                    }
                    else if (inlineCodeOn && strippedWord != "")
                    {
                        if (wrapCharacters)
                        {
                            foreach(char character in strippedWord)
                            {
                                AddLabel<InlineCodeLabel>(character.ToString(), elements, targetContainer);
                            }
                        }
                        else
                        {
                            if (previousElementWasAlsoInlineCode)
                            {
                                var l = AddLabel<InlineCodeLabel>(" ", elements, targetContainer);
                                l.style.paddingLeft = 0;
                            }
                            var l2 = AddLabel<InlineCodeLabel>(strippedWord, elements, targetContainer);
                            l2.style.paddingLeft = 0;
                            previousElementWasAlsoInlineCode = true;
                        }
                    }
                    else if (assetOn && strippedWord != "")
                    {
                        var label = new AssetlinkLabel
                        {
                            text = strippedWord,
                            tooltip = "Asset: " + strippedWord
                        };
                        label.RegisterCallback<MouseUpEvent, string>(
                            (evt, assetName) =>
                            {
                                TutorialEditorUtils.HighlightAsset(assetName);
                            },
                            strippedWord
                        );

                        targetContainer.Add(label);
                        elements.Add(label);
                    }
                    else if (gameObjectOn && strippedWord != "")
                    {
                        var label = new GameObjectlinkLabel
                        {
                            text = strippedWord,
                            tooltip = "Game Object: " + strippedWord
                        };
                        label.RegisterCallback<MouseUpEvent, string>(
                            (evt, gameObjectName) =>
                            {
                                TutorialEditorUtils.HighlightGameObject(gameObjectName);
                            },
                            strippedWord
                        );

                        targetContainer.Add(label);
                        elements.Add(label);
                    }
                    else if (docOn && strippedWord != "") //dumb but it will do the job :)
                    {
                        var label = new GameObjectlinkLabel
                        {
                            text = strippedWord,
                            tooltip = "Documentation: " + strippedWord
                        };
                        label.RegisterCallback<MouseUpEvent, string>(
                            (evt, gameObjectName) =>
                            {
                                TutorialEditorUtils.OpenUrl("https://docs.unity3d.com/ScriptReference/"+strippedWord+".html");
                            },
                            strippedWord
                        );

                        targetContainer.Add(label);
                        elements.Add(label);
                    }
                    else if (addParagraph)
                    {
                        elements.Add(AddParagraphToElement(targetContainer));
                    }
                    else if (linkOn && !string.IsNullOrEmpty(linkURL))
                    {
                        var label = new HyperlinkLabel
                        {
                            text = strippedWord,
                            tooltip = linkURL
                        };
                        label.RegisterCallback<MouseUpEvent, string>(
                            (evt, linkurl) =>
                            {
                                TutorialEditorUtils.OpenUrl(linkurl);
                            },
                            linkURL
                        );

                        targetContainer.Add(label);
                        elements.Add(label);
                    }
                    else
                    {
                        if (strippedWord != "")
                        {
                            if (wrapCharacters)
                            {
                                foreach (char character in strippedWord)
                                {
                                    AddLabel<TextLabel>(character.ToString(), elements, targetContainer);
                                }
                            }
                            else
                            {
                                var textLabel = AddLabel<TextLabel>(strippedWord, elements, targetContainer);
                                //if we just came from a bold block, then no padding on left
                                if (previousElementWasBold)
                                {
                                    textLabel.style.paddingLeft = 0;
                                }
                            }
                        }
                    }

                    if (!boldOn)
                        previousElementWasBold = false;
                    if (removeBold) boldOn = false;
                    if (removeItalic) italicOn = false;
                    if (removeCode) 
                    {
                        codeOn = false;
                        if (currentCodeBlock != "")
                        {
                            //remove annoying trailing new line (this could probaably be done better)
                            if (currentCodeBlock.Substring(currentCodeBlock.Length - 1) == "\n")
                                currentCodeBlock = currentCodeBlock.Substring(0, currentCodeBlock.Length - 1);
                            AddLabel<CodeLabel>(currentCodeBlock, elements, targetContainer);
                        }
                        currentCodeBlock= "";
                    }
                    if (removeInlineCode)
                    {
                        inlineCodeOn = false;
                        if (nextWord == string.Empty)
                        {
                            elements.Last().style.paddingRight = 0;
                            var space = AddLabel<TextLabel>(string.Empty, elements, targetContainer);
                            space.style.paddingRight = 0;
                        }
                        previousElementWasAlsoInlineCode = false;
                    }
                    if (removeAsset)
                    {
                        assetOn = false;
                        if (nextWord == string.Empty)
                        {
                            elements.Last().style.paddingRight = 0;
                            var space = AddLabel<TextLabel>(string.Empty, elements, targetContainer);
                            space.style.paddingRight = 0;
                        }
                    }
                    if (removeGameObject)
                    {
                        gameObjectOn = false;
                        if (nextWord == string.Empty)
                        {
                            elements.Last().style.paddingRight = 0;
                            var space = AddLabel<TextLabel>(string.Empty, elements, targetContainer);
                            space.style.paddingRight = 0;
                        }
                    }
                    if (removeDoc)
                    {
                        docOn = false;
                        if (nextWord == string.Empty)
                        {
                            elements.Last().style.paddingRight = 0;
                            var space = AddLabel<TextLabel>(string.Empty, elements, targetContainer);
                            space.style.paddingRight = 0;
                        }
                    }
                    if (removeLink)
                    {
                        linkOn = false;
                        linkURL = "";
                    }
                    if (removeWordWrap) forceWordWrap = false;

                    
                    if (currentCodeBlock != "" && codeOn)
                    {
                        currentCodeBlock+=" ";
                    }

                }
                firstLine = false;
                
                if (currentCodeBlock != "" && codeOn)
                {
                    currentCodeBlock+="\n";
                }
            }

            if (addError)
            {
                var label = new ParseErrorLabel()
                {
                    text = Localization.Tr("PARSE ERROR"),
                    tooltip = Localization.Tr("Click here to see more information in the console.")
                };
                label.RegisterCallback<MouseUpEvent>((e) => Debug.LogError(errorText));
                targetContainer.Add(label);
                elements.Add(label);
            }
            return elements;
        }

        static VisualElement AddLinebreakToElement(VisualElement elementTo)
        {
            Label wordLabel = new Label(" ");
            wordLabel.style.flexDirection = FlexDirection.Row;
            wordLabel.style.flexGrow = 1f;
            wordLabel.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            wordLabel.style.height = 8f;
            elementTo.Add(wordLabel);
            return wordLabel;
        }

        static VisualElement AddParagraphToElement(VisualElement elementTo)
        {
            Label wordLabel = new Label(" ");
            wordLabel.style.flexDirection = FlexDirection.Row;
            wordLabel.style.flexGrow = 1f;
            wordLabel.style.width = new StyleLength(new Length(100, LengthUnit.Percent));
            elementTo.Add(wordLabel);
            return wordLabel;
        }

        // Dummy classes so that we can customize the styles from a USS file.

        /// <summary>
        /// Label for the red parser error displayed where the parsing fails
        /// </summary>
        public class ParseErrorLabel : Label {}

        /// <summary>
        /// Text label for links
        /// </summary>
        public class HyperlinkLabel : Label {
            public HyperlinkLabel() : base()
            {
                this.AddToClassList("pointer");
            }
        }

        /// <summary>
        /// Text label for asset links
        /// </summary>
        public class AssetlinkLabel : Label
        {
            public AssetlinkLabel() : base()
            {
                this.AddToClassList("pointer");
            }
        }

        /// <summary>
        /// Text label for asset links
        /// </summary>
        public class GameObjectlinkLabel : Label
        {
            public GameObjectlinkLabel() : base()
            {
                this.AddToClassList("pointer");
            }
        }

        /// <summary>
        /// Text label for text that wraps per word
        /// </summary>
        public class TextLabel : Label
        {
            /// <summary>
            /// Constructs with text.
            /// </summary>
            /// <param name="text"></param>
            public TextLabel(string text) : base(text)
            {
            }
        }

        /// <summary>
        /// Text label for white space used to indent lines
        /// </summary>
        public class WhiteSpaceLabel : Label
        {
            /// <summary>
            /// Constructs with text.
            /// </summary>
            /// <param name="text"></param>
            public WhiteSpaceLabel(string text) : base(text)
            {
            }
        }

        /// <summary>
        /// Text label with bold style
        /// </summary>
        public class BoldLabel : Label
        {
            /// <summary>
            /// Constructs with text.
            /// </summary>
            /// <param name="text"></param>
            public BoldLabel(string text) : base(text)
            {
            }
        }

        /// <summary>
        /// Text label with italic style
        /// </summary>
        public class ItalicLabel : Label
        {
            /// <summary>
            /// Constructs with text.
            /// </summary>
            /// <param name="text"></param>
            public ItalicLabel(string text) : base(text)
            {
            }
        }

        public class InlineCodeLabel : Label 
        {
            public InlineCodeLabel(string text) : base(text)
            {
                this.EnableInClassList("code", true);
            }
        }

        public class CodeLabel : ScrollView//Label
        {
            /// <summary>
            /// Constructs with text.
            /// </summary>
            /// <param name="text"></param>
            public CodeLabel(string text) : base(ScrollViewMode.Horizontal)
            {
                var txt = new TextField("", int.MaxValue, true, false, '*');
                txt.value = text;
                txt.isReadOnly = true;
                txt.EnableInClassList("code", true);
                this.EnableInClassList("code_multiline", true);
                this.Add(txt);

                var container = this.Q("unity-content-container");
                container.style.paddingLeft = container.style.paddingRight = container.style.paddingTop = container.style.paddingBottom = 0;
            }
        }

    }
}
