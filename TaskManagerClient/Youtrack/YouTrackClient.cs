﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Threading;
using System.Web;
using SKBKontur.TaskManagerClient.Abstractions;
using System.Linq;
using SKBKontur.TaskManagerClient.Youtrack.BusinessObjects;

namespace SKBKontur.TaskManagerClient.Youtrack
{
    public class YouTrackClient : IBugTrackerClient
    {
        private readonly IHttpRequester httpRequester;
        private const string YouTrackBaseUrl = "https://yt.skbkontur.ru";
        private const string BugsIssueStartsString = "issue";
        private const string BugsIssueRestStartsString = "rest/issue";
        private const string BugsSprintStartsString = "rest/agile/";
        private const string UserLoginString = "rest/user/login";
        private Lazy<IEnumerable<Cookie>> authCookies;

        public YouTrackClient(IHttpRequester httpRequester, IYouTrackCredentialService youTrackCredentialService)
        {
            this.httpRequester = httpRequester;
            var credential = youTrackCredentialService.GetYouTrackCredentials();
            authCookies = new Lazy<IEnumerable<Cookie>>(() => GetAuthCookies(credential, httpRequester));
        }

        private static string BuildUrl(string queryPart)
        {
            return string.Format("{0}/{1}", YouTrackBaseUrl, queryPart);
        }

        public Issue[] GetFiltered(string filter)
        {
            var parameters = new Dictionary<string, string>
                                 {
                                     {"filter", filter},
                                     {"max", "1000"}
                                 };
            var result = httpRequester.SendGetAsync<YouTrackIssues>(BuildUrl(BugsIssueRestStartsString), parameters, authCookies.Value).Result;
            return result.Issue.Select(x =>
                                           {
                                               var lastComment = x.Comment.LastOrDefault();
                                               return new Issue
                                                           {
                                                               Id = x.Id,
                                                               CommentsCount = x.Comment.Length,
                                                               LastComment = lastComment != null ? lastComment.Text : null,
                                                               Created = x.SafeGetDateFromMilleseconds("created").Value,
                                                               Updated = x.SafeGetDateFromMilleseconds("updated").Value,
                                                               Description = x.SafeGet<string>("description"),
                                                               Summary = x.SafeGet<string>("summary"),
                                                               Resolved = x.SafeGetDateFromMilleseconds("resolved"),
                                                               CreatorLogin = x.SafeGet<string>("reporterName"),
                                                               CreatorFullName = x.SafeGet<string>("reporterFullName")
                                                           };
                                           }).ToArray();
        }

        public int GetFilteredCount(string filter)
        {
            var parameters = new Dictionary<string, string>
                                 {
                                     {"filter", filter}
                                 };
            var timer = Stopwatch.StartNew();
            while (true)
            {
                var countResult = httpRequester.SendGetAsync<EntityCount>(BuildUrl(BugsIssueRestStartsString + "/count"), parameters, authCookies.Value).Result;
                if (countResult.Value >= 0)
                {
                    timer.Stop();
                    return countResult.Value;
                }

                if (timer.ElapsedMilliseconds > 2000)
                {
                    timer.Stop();
                    return -1;
                }

                Thread.Sleep(100);
            }
        }
        
        public Issue[] GetSprintInfo(string sprintName)
        {
            var sprintFilter = string.Format("Fix versions:{{{0}}}", sprintName);
            return GetFiltered(sprintFilter);
        }

        public string GetIssueUrl()
        {
            return BuildUrl(BugsIssueStartsString) + "/";
        }

        public string GetSprintUrl()
        {
            return BuildUrl(BugsSprintStartsString);
        }

        public string GetStrintUrlEndWord()
        {
            return "/sprint/";
        }

        public string GetBaseUrl()
        {
            return YouTrackBaseUrl;
        }

        public string GetBrowseFilterUrl(string filter)
        {
            return BuildUrl(string.Format("issues?q={0}", HttpUtility.UrlEncode(filter)));
        }

        private static Cookie[] GetAuthCookies(YouTrackCredential credential, IHttpRequester httpRequester)
        {
            var credentials = new Dictionary<string, string>
                                  {
                                      {"login", credential.UserName},
                                      {"password", credential.Password},
                                  };
            return httpRequester.SendPostEncodedAsync(BuildUrl(UserLoginString), credentials).Result.ToArray();
        }
    }
}