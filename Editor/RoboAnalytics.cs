using System;
using System.Collections.Generic;
using UnityEngine;
using System.Net;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace Unity.Tutorials.Core.Editor
{
    public static class RoboAnalytics
    {
        public static string StudentID
        {
            get
            {
                return PlayerPrefs.GetString(RoboConfig.PlayerPrefsPrefix + "StudentID", "");
            }
            set
            {
                PlayerPrefs.SetString(RoboConfig.PlayerPrefsPrefix + "StudentID", value);
            }
        }
        public static string Username
        {
            get
            {
                return PlayerPrefs.GetString(RoboConfig.PlayerPrefsPrefix + "Username", "");
            }
            set
            {
                PlayerPrefs.SetString(RoboConfig.PlayerPrefsPrefix + "Username", value);
            }
        }

        public static async Task SaveTutorialSectionCompleted(TutorialContainer.Section section)
        {
            /*
            if (_cachedTutorialSectionsCompleted != null)
            {
                var arr = new string[_cachedTutorialSectionsCompleted.Length + 1];
                for (var i = 0; i < arr.Length - 1; i++)
                    arr[i] = _cachedTutorialSectionsCompleted[i];
                arr[arr.Length - 1] = section.TutorialId;
                _cachedTutorialSectionsCompleted = arr; 

                if (TutorialWindow.FindReadme().debugCompletionCache)
                    Debug.Log("SAVE _cachedTutorialSectionsCompleted IS NOW length " + _cachedTutorialSectionsCompleted.Length + " and values [" + string.Join(",", _cachedTutorialSectionsCompleted)+"]");

            }
            else
            {

                if (TutorialWindow.FindReadme().debugCompletionCache)
                    Debug.Log("SAVE _cachedTutorialSectionsCompleted not found, creating ");

                _cachedTutorialSectionsCompleted = new string[] { section.TutorialId };

                if (TutorialWindow.FindReadme().debugCompletionCache)
                    Debug.Log("SAVE _cachedTutorialSectionsCompleted IS NOW length " + _cachedTutorialSectionsCompleted.Length + " and values [" + string.Join(",", _cachedTutorialSectionsCompleted) + "]");

            }*/
            if (HasInternetConnection() == false) return;

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(RoboConfig.SECTION_PROGRESS_URL + "?post=true&tutorial=" + section.TutorialId + "&studentID=" + StudentID + "&username=" + Username);
            request.Method = "POST";
            await request.GetResponseAsync();
            
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    //Debug.Log("Publish Response: " + (int)response.StatusCode + ", " + response.ContentLength);
                }
            }
            catch (Exception e) { Debug.LogError(e); }
        }

        //static string[] _cachedTutorialSectionsCompleted = null;
        public static async Task<string[]> LoadTutorialSectionsCompleted()
        {
            /*
            if (_cachedTutorialSectionsCompleted != null && _cachedTutorialSectionsCompleted.Length > 0 && !_cachedTutorialSectionsCompleted.All(string.IsNullOrEmpty))
            {
                if (TutorialWindow.FindReadme().debugCompletionCache)
                    Debug.Log("_cachedTutorialSectionsCompleted found with length "+_cachedTutorialSectionsCompleted.Length+" and values ["+string.Join(",",_cachedTutorialSectionsCompleted) + "]");
                return _cachedTutorialSectionsCompleted;
            }
            else
            {
                if (TutorialWindow.FindReadme().debugCompletionCache)
                {
                    Debug.Log("_cachedTutorialSectionsCompleted NOT found, time to request");
                    if (_cachedTutorialSectionsCompleted != null)
                    {
                        Debug.Log("\t_cachedTutorialSectionsCompleted values [" + string.Join(",", _cachedTutorialSectionsCompleted) + "]");
                    }
                }
            }*/

            if (HasInternetConnection() == false) return new string[] { }; ;

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(RoboConfig.SECTION_PROGRESS_URL + "?studentID=" + StudentID + "&username=" + Username);
            //Debug.Log("url: " + request.RequestUri);
            request.Method = "GET";

            try
            {
                using (WebResponse wr = await request.GetResponseAsync())
                {
                    using (StreamReader sr = new StreamReader(wr.GetResponseStream()))
                    {
                        var json = sr.ReadToEnd();
                        //Debug.Log("JSON:" + json);
                        //argh could parse the json but just being lazy at this point;
                        json = json.Replace("\"", "").Replace("[", "").Replace("]", "");
                        var arr = json.Split(',');
                        /*_cachedTutorialSectionsCompleted = arr;

                        if (TutorialWindow.FindReadme().debugCompletionCache)
                            Debug.Log("CACHE: _cachedTutorialSectionsCompleted values [" + string.Join(",", _cachedTutorialSectionsCompleted) + "]");
                        */
                        return arr;
                    }
                }
            }
            catch { }
            return new string[] { };
        }

        public static void RecordProgress(string tutorial, string page)
        {
            if (HasInternetConnection() == false) return;
            //Debug.Log("Recorded Progress: '" + this.name + "' '" + CurrentPage.name + "' '"+System.Environment.UserName);

            HttpWebRequest request = (HttpWebRequest)HttpWebRequest.Create(RoboConfig.PROGRESS_URL + "?tutorial=" + tutorial + "&page=" + page + "&studentID=" + StudentID + "&username="+ Username);
            request.Method = "GET";
            request.GetResponseAsync();

            /*
            try
            {
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                {
                    //Debug.Log("Publish Response: " + (int)response.StatusCode + ", " + response.ContentLength);
                }
            }
            catch {  }*/
        }
        public static void RecordBasePackageDownload(string name)
        {
            RecordProgress(name, "BASE_PACKAGE"); //lazy lol
        }
        public static void RecordPageSkip(string name, string page)
        {
            RecordProgress(name, "SKIP_PAGE_"+page);
        }
        public static bool HasRecordedStudentIDAndName()
        {
            return PlayerPrefs.HasKey(RoboConfig.PlayerPrefsPrefix + "StudentID") &&
                    PlayerPrefs.HasKey(RoboConfig.PlayerPrefsPrefix + "Username") &&
                    !string.IsNullOrEmpty(PlayerPrefs.GetString(RoboConfig.PlayerPrefsPrefix + "StudentID")) &&
                    !string.IsNullOrEmpty(PlayerPrefs.GetString(RoboConfig.PlayerPrefsPrefix + "Username"));
        }
        public static void SaveNameAndID(string username, string id)
        {
            Username = username;
            StudentID = id;
        }
        //TODO: clear/reset username and student id in case of mistake. blah.

        public static bool HasInternetConnection()
        {
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                Debug.LogWarning("No internet connection detected! Progress won't save.");
                return false;
            }
            return true;
        }
    }
}