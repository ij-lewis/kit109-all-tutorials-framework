using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Unity.Tutorials.Core.Editor;
using UnityEditor;
using UnityEngine;
using System;
using System.Linq;

public static class ExportTutorial
{
    static string htmlBaseFileName = "export.html"; 
    static string path;
    static int currentTuteIndex = -1;
    [MenuItem(TutorialWindowMenuItem.MenuPath + "Export as HTML", priority = 10)] 
    public static void ExportMenuItem() { Export(); }
    public static void Export(string defaultFilename = "export")
    {
        path = EditorUtility.SaveFilePanel(
            "Export Location",
            "",
            defaultFilename,
            "html");
        if (string.IsNullOrEmpty(path)) return; //cancelled

        htmlBaseFileName = Path.GetFileName(path);

        if (path.Length != 0)
        {
            var tutorials = TutorialWindow.FindReadme();
            if (tutorials.tabNames == null || tutorials.tabNames.Count == 0) //only one tutorial in project
            {
                currentTuteIndex = -1;

                //TODO: test this version
                var html = ExportIndividualTutorial(tutorials.Sections.ToList(), tutorials.Title, single:true);

                File.WriteAllText(path, html);
                OpenWithDefaultProgram(path);
            }
            else // a page for each tute, and a index page
            {

                var welcome = FindWelcomePage();

                var output = StartOutput(welcome.Title);
                output += p(welcome.Description, "description");
                output += divider();

                output += "<ul id=\"toc\">";
                for (var i = 0; i < tutorials.tabNames.Count; i++)
                {
                    currentTuteIndex = i;

                    var tab = tutorials.tabNames[i] + " - "+ tutorials.tabTooltips[i];
                    var tutorialList = tutorials.Sections.Where(s => s.kit109Tutorial == i).ToList();
                    var page = SavePage(tutorials.tabNames[i], ExportIndividualTutorial(tutorialList, tab));
                    output += "<li>" +
                        "<a href=\""+page+"\">" + tab + "</a>" +
                    "</li>";
                }
                output += "</ul>";
                output += EndOutput();

                File.WriteAllText(path, output);
                OpenWithDefaultProgram(path);
            }
        }

    }
    static string StartOutput(string title)
    {
        var output = "<html>" +
            "<head>" +
            "<title>"+title+" - KIT109 Games Fundamentals</title>" +
            "<link rel=\"stylesheet\" href=\"http://131.217.172.176/static/style.css\" />" +
            "</head>" +
            "<body id=\"exportTutorial\">" +
            "<div id=\"top\">";

        if (!string.IsNullOrEmpty(title))
        {
            output += h1(title);
        }
        return output;
    }
    static string EndOutput()
    {
        var year = DateTime.Now.Year;
        return 
            "<footer>&copy; "+year+" University of Tasmania. Written by Dr Lindsay Wells and Dr Ian Lewis.</footer>" +
            "</div>" +
            "</body>";
    }
    static string ExportIndividualTutorial(List<TutorialContainer.Section> tutorialList, string title = null, bool single = false) //returns the html content
    {
        var output = StartOutput(title);

        if (single)
        {
            var welcome = FindWelcomePage();
            output += p(welcome.Description, "description");
            output += divider();
        }

        //table of contents 
        output += h2("Table of Contents");

        int sectionID = 0;
        output += "<ol id=\"toc\">";
        foreach (var section in tutorialList)
        {
            //heading of whole page
            output += li(a(section.Heading, href:"#"+sectionID));
            sectionID++;
        }
        output += "</ol>\n";

        output += divider();

        //each tutorial, dumped
        sectionID = 0;
        foreach (var section in tutorialList)
        {
            output += h2(section.Heading, id:sectionID.ToString());
            if (!string.IsNullOrEmpty(section.Url))
            {
                output += a(section.Text, section.Url, "description");
            }
            else
            {
                output += p(section.Text, "description");


                //each page, dumped
                var tutorial = section.Tutorial;
                if (tutorial != null && tutorial.Pages != null)
                {
                    int pageID = 0;
                    foreach (var page in tutorial.Pages)
                    {
                        int paraID = 0;
                        foreach (var para in page.Paragraphs)
                        {
                            var text = para.Text.Translated;
                            if (!string.IsNullOrEmpty(text))
                            {
                                //tabs
                                text = text.Replace("\t", "<span class=\"tabChar\"></span>");
                                //code blocks
                                text = text.Replace("<code>", "</p><pre>");
                                text = text.Replace("</code>", "</pre><p>");

                                //inline code
                                text = text.Replace("<c>", "<span style=\"font-family:consolas, 'Courier New', monospace\">");
                                text = text.Replace("</c>", "</span>");

                                //links
                                text = Regex.Replace(text, @"\<a\>(.*?)\</a>",
                                    m =>
                                    {
                                        var urlString = m.Groups[1].Value;

                                        // then you have to evaluate this string
                                        return "<a target=\"_blank\" href=\"" + urlString + "\">" + urlString + "</a>";
                                    });

                                //doc links
                                text = Regex.Replace(text, @"\<doc\>(.*?)\</doc>",
                                    m =>
                                    {
                                        var urlString = m.Groups[1].Value;

                                        // then you have to evaluate this string
                                        return "<a target=\"_blank\" href=\"https://docs.unity3d.com/ScriptReference/" + urlString + ".html\">" + urlString + "</a>";
                                    });
                            }

                            switch (para.Type)
                            {
                                case ParagraphType.Narrative:
                                    if (!string.IsNullOrEmpty(para.Title))
                                    {
                                        output += h3(para.Title);
                                    }
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        output += p(text);
                                    }
                                    break;

                                case ParagraphType.Instruction:
                                    output += "<div class=\"instruction\">";
                                    if (!string.IsNullOrEmpty(para.Title))
                                    {
                                        output += tag("b", para.Title);
                                        output += "<br />";
                                    }
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        output += p(text);
                                    }
                                    output += "</div>";
                                    break;

                                case ParagraphType.Image:
                                    if (para.Image == null) continue;
                                    output += "<div class=\"img\">";
                                    output += "<img src=\"" + SaveImage(page, para.Image) + "\" style=\"max-width:" + (para.Image.height * 4) + "px\" />";
                                    output += "</div>";
                                    break;

                                case ParagraphType.Poll:
                                    //TODO: make these interactive
                                    output += "<div class=\"instruction\">";
                                    if (!string.IsNullOrEmpty(para.Title))
                                    {
                                        output += tag("b", para.Title);
                                        output += "<br />";
                                    }
                                    if (!string.IsNullOrEmpty(text))
                                    {
                                        output += p(text);
                                    }
                                    output += "<ul class=\"poll\">"; //TODO: make these not uggers
                                    foreach (var item in para.m_PollItems)
                                    {
                                        output += "<li><label><input type=\"radio\" name=\"poll_"+pageID+"_"+paraID+"\" />" + item + "</label></li>";
                                    }
                                    output += "</ul>";
                                    output += "</div>";
                                    break;

                                default:
                                    Debug.LogError("Unsupported export paragraph type: " + para.Type);
                                    break;
                            }
                            paraID++;
                        }
                        pageID++;
                    }
                }
            }

            output += divider();

            sectionID++;
        }

        output += EndOutput();

        return output;
    }

    public static void OpenWithDefaultProgram(string path)
    {
        System.Diagnostics.Process.Start(path);
    }


    static string tag(string tag, string text, string className = "", string id = "")
    {
        return string.Format("<{0} id=\"{3}\" class=\"{2}\">{1}</{0}>", tag, text, className ?? "", id ?? "");
    }
    static string h1(string text, string id = "", string className = "")
    {
        return tag("h1", text, className, id);
    }
    static string h2(string text, string id = "", string className = "")
    {
        return tag("h2", text, className, id);
    }
    static string h3(string text, string id = "", string className = "")
    {
        return tag("h3", text, className, id);
    }
    
    static string a(string text, string href, string className = "")
    {
        return string.Format("<a href=\"{0}\" class=\"{2}\">{1}</a>", href, text, className ?? "");
    }

    static string li(string text)
    {
        return tag("li", text);
    }
    static string p(string text, string className = "")
    {
        text = text.Replace("<code>", "<pre>");
        text = text.Replace("</code>", "</pre>");

        //need to split out code chunks
        var chunksItr = text.SplitAndKeep("<pre>", "</pre>");
        var chunks = new List<string>();
        foreach (var c in chunksItr) { chunks.Add(c); }
        bool nextIsPre = false;
        for (var j = 0; j < chunks.Count; j++)
        {
            if (nextIsPre == false)
            {
                if (chunks[j].IndexOf("<pre>") >= 0)
                {
                    nextIsPre = true;
                }

                chunks[j] = chunks[j].Replace("\n<pre>", "");
                chunks[j] = chunks[j].Replace("<pre>\n", "");
                chunks[j] = chunks[j].Replace("<pre>", "");

                //nl2br
                var lines = chunks[j].Split('\n');
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    lines[i] = "<p class=\""+className+"\">" + line + "</p>";
                }

                chunks[j] = string.Join("", lines);
            }
            else //pre block, don't br the links inside
            {
                chunks[j] = "<pre>" + chunks[j];
                nextIsPre = false;
            }
        }
        return string.Join("", chunks);
    }

    static string divider()
    {
        return p("[ "+a("Back to Top", href:"#top")+" ]", className:"backToTop")+"<hr />";
    }


    static TutorialWelcomePage FindWelcomePage()
    {
        var ids = AssetDatabase.FindAssets($"t:{typeof(TutorialWelcomePage).FullName}");
        return ids.Length == 1
            ? (TutorialWelcomePage)AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids[0]))
            : null;
    }

    public static IEnumerable<string> SplitAndKeep(this string s, params string[] delims)
    {
        var rows = new List<string>() { s };
        foreach (string delim in delims)//delimiter counter
        {
            for (int i = 0; i < rows.Count; i++)//row counter
            {
                int index = rows[i].IndexOf(delim);
                if (index > -1
                    && rows[i].Length > index + 1)
                {
                    string leftPart = rows[i].Substring(0, index + delim.Length);
                    string rightPart = rows[i].Substring(index + delim.Length);
                    rows[i] = leftPart;
                    rows.Insert(i + 1, rightPart);
                }
            }
        }
        return rows;
    }

    static string ImagesFolder()
    {
        return htmlBaseFileName.Substring(0, htmlBaseFileName.LastIndexOf(".")) + "_images";
    }
    static string SaveImage(TutorialPage page, Texture2D image) //and return the path
    {
        var assetPath = AssetDatabase.GetAssetPath(image);
        var filename = page.name + "_" + image.name + "." + Path.GetExtension(assetPath);
        if (currentTuteIndex != -1)
        {
            filename = currentTuteIndex + "_" + filename;
        }
        var savePath = Path.Combine(Path.GetDirectoryName(path), ImagesFolder(), filename);
        var relativePath = Path.Combine(ImagesFolder(), filename);

        var imagesFolderPath = Path.Combine(Path.GetDirectoryName(path), ImagesFolder());
        if (Directory.Exists(imagesFolderPath) == false)
        {
            Directory.CreateDirectory(imagesFolderPath);
        }

        File.Copy(assetPath, savePath, overwrite: true);

        if (currentTuteIndex != -1)
        {
            relativePath = "../" + relativePath;
        }
        return relativePath;
    }
    static string PagesFolder()
    {
        return htmlBaseFileName.Substring(0, htmlBaseFileName.LastIndexOf(".")) + "_pages";
    }
    static string SavePage(string tutorialName, string html) //and return the path
    {
        var filename = tutorialName + ".html";
        var savePath = Path.Combine(Path.GetDirectoryName(path), PagesFolder(), filename);
        var relativePath = Path.Combine(PagesFolder(), filename);

        var imagesFolderPath = Path.Combine(Path.GetDirectoryName(path), PagesFolder());
        if (Directory.Exists(imagesFolderPath) == false)
        {
            Directory.CreateDirectory(imagesFolderPath);
        }

        File.WriteAllText(savePath, html);

        return relativePath;
    }
}
