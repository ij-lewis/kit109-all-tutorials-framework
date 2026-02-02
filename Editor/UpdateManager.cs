using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.IO.Compression;
using System.Threading;
using Unity.SharpZipLib.Zip;

namespace Unity.Tutorials.Core.Editor
{
    public static class UpdateManager
    {
        [MenuItem("Tutorials/Check for Update...")]
        public static async void MenuItemCheckForUpdate()
        {
            bool updateFound = false;

            var systemUpdate = await UpdateManager.CheckTutorialSystemUpdateAvailable();
            if (systemUpdate)
            {
                updateFound = true;
                if (EditorUtility.DisplayDialog("Tutorial System", "Tutorial System Update Available (Version " + (UpdateManager.LatestTutorialSystemVersion) + ")", "Download", "Cancel"))
                {
                    await UpdateManager.DownloadTutorialSystemUpdate();
                }
            }
            else
            {
                var update = await UpdateManager.CheckTutorialUpdateAvailable();
                if (update)
                {
                    updateFound = true;
                    if (EditorUtility.DisplayDialog("Tutorial System", "Tutorial Content Update Available (Version " + (UpdateManager.CurrentTutorialVersion + 1) + ")", "Download", "Cancel"))
                    {
                        await UpdateManager.DownloadTutorialUpdate();
                    }
                }
            }

            if (updateFound == false)
            {
                EditorUtility.DisplayDialog("Tutorial System", "No Update Found", "OK");
            }
        }

        public static int CurrentTutorialVersion
        {
            get
            {
                //add check here to see if the old player prefs one is actually newer, preventing a revert back to version 1
                var playerPrefsOldVersion = PlayerPrefs.GetInt(RoboConfig.PlayerPrefsPrefix + "TutorialVersion", 0);
                var scriptableObjNewVersion = TutorialWindow.FindReadme().currentTutorialContentVersion;

                return Mathf.Max(playerPrefsOldVersion, scriptableObjNewVersion);
            }
            set
            {
                var tuteScript = TutorialWindow.FindReadme();
                tuteScript.currentTutorialContentVersion = value;
                EditorUtility.SetDirty(tuteScript);
                AssetDatabase.SaveAssets();
            }
        }
        public static int CurrentTutorialSystemVersion
        {
            get
            {
                //add check here to see if the old player prefs one is actually newer, preventing a revert back to version 1
                var playerPrefsOldVersion = PlayerPrefs.GetInt(RoboConfig.PlayerPrefsPrefix + "TutorialSystemVersion", 0);
                var scriptableObjNewVersion = TutorialWindow.FindReadme().currentTutorialSystemVersion;

                return Mathf.Max(playerPrefsOldVersion, scriptableObjNewVersion);
            }
            set
            {
                var tuteScript = TutorialWindow.FindReadme();
                tuteScript.currentTutorialSystemVersion = value;
                EditorUtility.SetDirty(tuteScript);
                AssetDatabase.SaveAssets();
            }
        }
        static int CurrentTutorialVersionStoredInScriptableObject
        {
            get
            {
                return TutorialWindow.FindReadme().currentTutorialContentVersion;
            }
        }
        public static int LatestTutorialSystemVersion  //this gets updated only after CheckTutorialSystemUpdateAvailable returns true;
        {
            get
            {
                return onlineSystemVersion;
            }
        }

        public static async Task<bool> CheckTutorialUpdateAvailable()
        {
            if (ProjectMode.IsAuthoringMode()) return false; //dont show updates in authoring mode, annoying

            if (RoboAnalytics.HasInternetConnection() == false) return false;

            //EditorUtility.DisplayProgressBar("Checking For Tutorial Update", "Please Wait", 1);

            try
            {
                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(RoboConfig.UPDATE_URL + RoboConfig.PlayerPrefsPrefix + "/version.txt");
                request.Method = "GET";

                using (var wr = await TimeoutAfter(request.GetResponseAsync(), new TimeSpan(0, 0, 1)))
                //using (WebResponse wr = await request.GetResponseAsync())
                {
                    using (StreamReader sr = new StreamReader(wr.GetResponseStream()))
                    {
                        var ONLINE_VERSION = int.Parse(sr.ReadToEnd());
                        var result = ONLINE_VERSION > CurrentTutorialVersion;
                        EditorUtility.ClearProgressBar();

                        return result;
                    }
                }

            }
            catch { }

            EditorUtility.ClearProgressBar();
            return false;
        }


        public static async Task<TResult> TimeoutAfter<TResult>(this Task<TResult> task, TimeSpan timeout)
        {

            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {

                var completedTask = await Task.WhenAny(task, Task.Delay(timeout, timeoutCancellationTokenSource.Token));
                if (completedTask == task)
                {
                    timeoutCancellationTokenSource.Cancel();
                    return await task;  // Very important in order to propagate exceptions
                }
                else
                {
                    //Debug.Log("timed out");
                    throw new TimeoutException("The operation has timed out.");
                }
            }
        }

        public static async Task<bool> CheckTutorialSystemUpdateAvailable()
        {
            if (ProjectMode.IsAuthoringMode()) return false; //dont show updates in authoring mode, annoying

            if (RoboAnalytics.HasInternetConnection() == false) return false;

            //EditorUtility.DisplayProgressBar("Checking For Tutorial System Update", "Please Wait", 1);
            try
            {
                //Debug.Log("ServicePointManager.DefaultConnectionLimit: "+ ServicePointManager.DefaultConnectionLimit);

                ServicePointManager.DefaultConnectionLimit = 100;

                HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(RoboConfig.UPDATE_URL + "/tutorialSystem/version.txt");
                request.Method = "GET";
                //request.Timeout = 1000; //useless, ignored by GetResponseAsync :(

                //Debug.Log("<color=#FF4400>" + RoboConfig.UPDATE_URL + "/tutorialSystem/version.txt " + "</color>");

                //Debug.Log("<color=#FF4400>STEP 0</color>");
                using (var wr = await TimeoutAfter(request.GetResponseAsync(), new TimeSpan(0, 0, 1)))

                //using (WebResponse wr = await request.GetResponseAsync())
                {
                    //Debug.Log("<color=#FF4400>STEP 1</color>");
                    using (StreamReader sr = new StreamReader(wr.GetResponseStream()))
                    {
                        //Debug.Log("<color=#FF4400>STEP 2</color>");
                        var ONLINE_VERSION = int.Parse(sr.ReadToEnd());
                        var result = ONLINE_VERSION > CurrentTutorialSystemVersion;
                        onlineSystemVersion = ONLINE_VERSION;
                        EditorUtility.ClearProgressBar();

                        return result;
                    }
                }
            }
            catch (TimeoutException)
            {
                //Debug.Log("timed out (second message)");
                return false;
            }
            catch 
            {
                //Debug.Log("something went boom");
            }

            EditorUtility.ClearProgressBar();
            return false;
        }


        public static async Task DownloadTutorialUpdate()
        {
            var tutorialCONTENTVersion = CurrentTutorialVersion; //catch this for later **
            var tutorialSYSTEMVersion = CurrentTutorialSystemVersion; //cache this for later *
            //if (EditorUtility.DisplayDialog("Tutorial Update", "After the download is complete make sure you click *Import* in the popup that appears.", "OK I WILL CLICK IMPORT IN THE POPUP THAT APPEARS", "No, I don't think I will..."))
            {
                EditorUtility.DisplayProgressBar("Downloading Tutorial Update", "Please Wait", 1);

                //we no longer do this, we rely on the Tutorial asset being up to date
                //CurrentTutorialVersion++; //increment through

                try
                {
                    var downloadLocation = Application.temporaryCachePath + "/update.unitypackage";
                    WebClient webClient = new WebClient();
                    webClient.Headers.Add("Accept: text/html, application/xhtml+xml, */*");
                    webClient.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");
                    await webClient.DownloadFileTaskAsync(new Uri(RoboConfig.UPDATE_URL + "/" + RoboConfig.PlayerPrefsPrefix + "/" + (CurrentTutorialVersion + 1) + ".unitypackage"), downloadLocation);

                    //import the package
                    EditorUtility.DisplayProgressBar("Importing Tutorial Update", "Please Wait", 1);
                    AssetDatabase.importPackageCompleted += async (info) =>
                    {
                        //* now that this is complete, we should restore the (definitely) overwritten tutorial system version (this is stored on the TutorialContainer obj now)
                        CurrentTutorialSystemVersion = tutorialSYSTEMVersion;

                        //** now that this is complete, check to see if we are downloading an early-mode update, where the version number wasn't stored
                        //we can tell this because tutorialCONTENTVersion will be the same as before
                        //we fix this by making it in line now
                        Debug.Log("CurrentTutorialVersionStoredInScriptableObject " + CurrentTutorialVersionStoredInScriptableObject + " tutorialCONTENTVersion " + tutorialCONTENTVersion);
                        if (CurrentTutorialVersionStoredInScriptableObject <= tutorialCONTENTVersion)
                        {
                            CurrentTutorialVersion = tutorialCONTENTVersion + 1;
                        }

                        //check for the next update
                        EditorUtility.DisplayProgressBar("Checking For Tutorial Update", "Please Wait", 1);
                        await CheckTutorialUpdateAvailable();
                        EditorUtility.ClearProgressBar();

                        EditorUtility.DisplayDialog("Tutorial Content Update", "Tutorial Content Update Successful!", "OK");
                    };
                    AssetDatabase.ImportPackage(downloadLocation, interactive: false);

                    //Application.OpenURL(downloadLocation);
                }
                catch (Exception e)
                {
                    EditorUtility.DisplayDialog("Tutorial Content Update", "Error: " + e.Message, "Ok, I should tell Lindsay");
                    Debug.LogError(e);
                }

            }
        }
        static int onlineSystemVersion;
        public static async Task DownloadTutorialSystemUpdate()
        {
            //if (EditorUtility.DisplayDialog("Tutorial System Update", "After the download is complete make sure you click *Import* in the popup that appears.", "OK I WILL CLICK IMPORT IN THE POPUP THAT APPEARS", "No, I don't think I will..."))
            //{
            EditorUtility.DisplayProgressBar("Downloading Tutorial System Update", "Please Wait", 1);
            CurrentTutorialSystemVersion = onlineSystemVersion;

            try
            {
                var downloadLocation = Application.temporaryCachePath + "/iet.zip";
                WebClient webClient = new WebClient();
                webClient.Headers.Add("Accept: text/html, application/xhtml+xml, */*");
                webClient.Headers.Add("User-Agent: Mozilla/5.0 (compatible; MSIE 9.0; Windows NT 6.1; WOW64; Trident/5.0)");
                await webClient.DownloadFileTaskAsync(new Uri(RoboConfig.UPDATE_URL + "/tutorialSystem/" + CurrentTutorialSystemVersion + ".zip"), downloadLocation);

                //Debug.Log(downloadLocation);
                FastZip fastZip = new FastZip();
                string fileFilter = null;

                // Will always overwrite if target filenames already exist
                fastZip.ExtractZip(downloadLocation, Application.dataPath + "/../", fileFilter);
            }
            catch (Exception e)
            {
                EditorUtility.DisplayDialog("Tutorial SYstem Update", "Error: " + e.Message, "Ok, I should tell Lindsay");
                Debug.LogError(e);
            }

            EditorUtility.ClearProgressBar();
            //}
        }
    }
}