﻿using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using SKBKontur.HttpInfrastructure.Clients;
using SKBKontur.TaskManagerClient.Repository.Clients.BusinessObjects;

namespace SKBKontur.TaskManagerClient.Repository.Clients.GitLab
{
    public class GitLabClient : IRepositoryClient
    {
        private readonly string repoId;
        private readonly IHttpClient httpClient;
        private readonly Dictionary<string, string> credentialParameters;
        private readonly string gitLabDefaultUrl;

        public GitLabClient(string repoId, IHttpClient httpClient, IGitLabCredentialService gitLabCredentialService)
        {
            this.repoId = repoId;
            this.httpClient = httpClient;
            var credentials = gitLabCredentialService.GetGitLabCredentials();
            gitLabDefaultUrl = credentials.DefaultUrl;
            credentialParameters = new Dictionary<string, string>
                                   {
                                       {"private_token", credentials.PrivateToken},
                                       {"sudo", credentials.UserName}
                                   };
        }

        public RepoCommit[] SelectLastBranchCommits(string branchName, int pageNumber, int pageSize)
        {
            var parameters = new Dictionary<string, string>(credentialParameters)
                                 {
                                     {"ref_name", branchName},
                                     {"page", pageNumber.ToString(CultureInfo.InvariantCulture)},
                                     {"per_page", pageSize.ToString(CultureInfo.InvariantCulture)},
                                 };
            return httpClient.SendGetAsync<RepoCommit[]>($"{gitLabDefaultUrl}/api/v3/projects/{repoId}/repository/commits", parameters).Result;
        }

        public RepoBranch[] SelectAllBranches()
        {
            var parameters = new Dictionary<string, string>(credentialParameters)
                                 {
                                     {"per_page", "1000"},
                                 };

            return httpClient.SendGetAsync<RepoBranch[]>($"{gitLabDefaultUrl}/api/v3/projects/{repoId}/repository/branches", parameters).Result;
        }
    }
}