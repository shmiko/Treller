using System;
using System.Collections.Generic;
using System.Linq;
using SKBKontur.BlocksMapping.Attributes;
using SKBKontur.Infrastructure.CommonExtenssions;
using SKBKontur.TaskManagerClient;
using SKBKontur.Treller.WebApplication.Blocks.Builders;
using SKBKontur.Treller.WebApplication.Blocks.TaskDetalization.Models;
using SKBKontur.Treller.WebApplication.Blocks.TaskList.Blocks;
using SKBKontur.Treller.WebApplication.Blocks.TaskList.ViewModels;
using SKBKontur.BlocksMapping.BlockExtenssions;
using SKBKontur.TaskManagerClient.BusinessObjects.TaskManager;
using SKBKontur.Treller.WebApplication.Implementation.Infrastructure.Extensions;
using SKBKontur.Treller.WebApplication.Implementation.Services.Settings;
using SKBKontur.Treller.WebApplication.Implementation.Services.TaskCacher;
using WebGrease.Css.Extensions;

namespace SKBKontur.Treller.WebApplication.Blocks.TaskList.Builders
{
    public class TaskListBuilder
    {
        private readonly ITaskManagerClient taskManagerClient;
        private readonly ISettingService settingService;
        private readonly IUserAvatarViewModelBuilder userAvatarViewModelBuilder;
        private readonly ICardStageInfoBuilder cardStageInfoBuilder;
        private readonly ITaskCacher taskCacher;
        private readonly IReleaseCandidateService releaseCandidateService;
        private readonly IBugTrackerClient bugTrackerClient;
        private readonly IWikiClient wikiClient;
        private readonly IBugsBuilder bugsBuilder;

        public TaskListBuilder(ITaskManagerClient taskManagerClient,
                               ISettingService settingService,
                               IUserAvatarViewModelBuilder userAvatarViewModelBuilder,
                               ICardStageInfoBuilder cardStageInfoBuilder,
                               ITaskCacher taskCacher,
                               IReleaseCandidateService releaseCandidateService,
                               IBugTrackerClient bugTrackerClient,
                               IWikiClient wikiClient,
                               IBugsBuilder bugsBuilder)
        {
            this.taskManagerClient = taskManagerClient;
            this.settingService = settingService;
            this.userAvatarViewModelBuilder = userAvatarViewModelBuilder;
            this.cardStageInfoBuilder = cardStageInfoBuilder;
            this.taskCacher = taskCacher;
            this.releaseCandidateService = releaseCandidateService;
            this.bugTrackerClient = bugTrackerClient;
            this.wikiClient = wikiClient;
            this.bugsBuilder = bugsBuilder;
        }

        [BlockModel(ContextKeys.TasksKey)]
        public Dictionary<string, BoardSettings> BuildSettings(CardListEnterModel enterModel)
        {
            if (enterModel == null || enterModel.BoardIds == null || enterModel.BoardIds.Length == 0)
            {
                return settingService.GetDevelopingBoards().ToDictionary(x => x.Id);
            }

            return settingService.GetDevelopingBoards().Where(x => enterModel.BoardIds.Contains(x.Id)).ToDictionary(x => x.Id);
        }

        [BlockModel(ContextKeys.TasksKey)]
        [BlockModelParameter("boardIds")]
        private string[] BuildSettings(Dictionary<string, BoardSettings> settings)
        {
            return settings.Select(x => x.Key).ToArray();
        }

        [BlockModel(ContextKeys.TasksKey)]
        [BlockModelParameter("rcBranches")]
        public SimpleRepoBranch[] BuildReleaseCandidateBranches()
        {
            return releaseCandidateService.WhatBranchesInReleaseCandidate();
        }

        [BlockModel(ContextKeys.TasksKey)]
        public SimpleRepoBranch[] BuildReleaseCandidateReleasedBranches([BlockModelParameter("rcBranches")] SimpleRepoBranch[] rcBranches)
        {
            var isReleased = releaseCandidateService.CheckForReleased(rcBranches);
            rcBranches.ForEach(x => x.IsReleased = isReleased[x.Name]);
            return rcBranches.Where(x => !x.IsReleased).ToArray();
        }

        [BlockModel(ContextKeys.TasksKey)]
        private BoardCard[] BuildCards([BlockModelParameter("boardIds")] string[] boardIds)
        {
            return taskCacher.GetCached(boardIds, ids => taskManagerClient.GetBoardCardsAsync(ids).Result, TaskCacherStoredTypes.BoardCards);
        }

        [BlockModel(ContextKeys.TasksKey)]
        private ILookup<string, BoardList> BuildBoardLists([BlockModelParameter("boardIds")] string[] boardIds)
        {
            return taskCacher.GetCached(boardIds, ids => taskManagerClient.GetBoardListsAsync(ids).Result, TaskCacherStoredTypes.BoardLists).ToLookup(x => x.BoardId);
        }

        [BlockModel(ContextKeys.TasksKey)]
        private Dictionary<string, User> BuildUsers([BlockModelParameter("boardIds")] string[] boardIds)
        {
            return taskCacher.GetCached(boardIds, ids => taskManagerClient.GetBoardUsersAsync(ids).Result, TaskCacherStoredTypes.BoardUsers).ToDictionary(x => x.Id);
        }

        [BlockModel(ContextKeys.TasksKey)]
        private ILookup<string, CardAction> BuildCardActions([BlockModelParameter("boardIds")] string[] boardIds)
        {
            return taskCacher.GetCached(boardIds, ids => taskManagerClient.GetActionsForBoardCardsAsync(ids).Result, TaskCacherStoredTypes.BoardActions).ToLookup(x => x.CardId);
        }

        [BlockModel(ContextKeys.TasksKey)]
        private Board[] BuildBoards([BlockModelParameter("boardIds")] string[] boardIds)
        {
            return taskCacher.GetCached(boardIds, ids => taskManagerClient.GetBoardsAsync(ids).Result, TaskCacherStoredTypes.Boards);
        }

        [BlockModel(ContextKeys.TasksKey)]
        private ILookup<string, CardChecklist> BuildCardChecklists([BlockModelParameter("boardIds")] string[] boardIds)
        {
            return taskCacher.GetCached(boardIds, ids => taskManagerClient.GetBoardChecklistsAsync(ids).Result, TaskCacherStoredTypes.BoardChecklists).ToLookup(x => x.CardId);
        }

        [BlockModel(ContextKeys.TasksKey)]
        private CardStateOverallViewModel[] BuildCards(BoardCard[] cards, Dictionary<string, User> users, ILookup<string, BoardList> boardLists, 
                                                   Dictionary<string, BoardSettings> boardSettings, ILookup<string, CardAction> cardActions,
                                                   ILookup<string, CardChecklist> cardChecklists, SimpleRepoBranch[] branches,
                                                   Dictionary<string, BugsInfoViewModel> bugs, CardListEnterModel cardListEnterModel)
        {
            var rcBranches = new HashSet<string>(branches.Select(x => x.Name));
            return cards.Where(x => !x.Name.Contains("���������", StringComparison.OrdinalIgnoreCase))
                        .Select(card => BuildCard(users, boardLists, boardSettings, cardActions, cardChecklists, card, rcBranches, bugs.SafeGet(card.Id)))
                        .Where(x => x.StageInfo.State != CardState.BeforeDevelop && x.StageInfo.State != CardState.Unknown && x.StageInfo.State != CardState.Released)
                        .Where(x => cardListEnterModel.ShowMode == ShowMode.All || 
                                    (cardListEnterModel.ShowMode == ShowMode.Infrastructure 
                                     ? x.Labels.Any(l => l.Color == CardLabelColor.Blue)
                                     : x.Labels.All(l => l.Color != CardLabelColor.Blue)))
                        .OrderByDescending(x => x.StageInfo.State)
                        .ThenByDescending(x => x.StageInfo.StageParrots.PastDays)
                        .ThenBy(x => x.StageInfo.StageParrots.BeginDate)
                        .GroupBy(x => x.StageInfo.State)
                        .Select(x => new CardStateOverallViewModel
                                        {
                                            Cards = x.ToArray(),
                                            State = x.Key,
                                            Title = x.Key.GetEnumDescription()
                                        })
                        .ToArray();
        }

        [BlockModel(ContextKeys.TasksKey)]
        private Dictionary<string, BugsInfoViewModel> BuildBugsInfo(ILookup<string, CardChecklist> cardChecklists)
        {
            return cardChecklists.ToDictionary(x => x.Key, x => bugsBuilder.Build(x));
        }
        #region remove hardcode inside

        [BlockModel(ContextKeys.TasksKey)]
        [BlockModelParameter("battleBugsCount")]
        private BugsCountLinkInfoViewModel BuildBattleBugsInfo()
        {
            return BuildCountLink("#Billy #Battle #Unresolved", "��� ���� � ������ ��������");
        }

        [BlockModel(ContextKeys.TasksKey)]
        [BlockModelParameter("battleBugsUnassignedCount")]
        private BugsCountLinkInfoViewModel BuildBattleBugsUnassignedInfo()
        {
            return BuildCountLink("#Billy #Battle #Unassigned #Unresolved", "������������� ���� � ������");
        }

        [BlockModel(ContextKeys.TasksKey)]
        [BlockModelParameter("currentBillyBugsCount")]
        private BugsCountLinkInfoViewModel GetBillyCurrentBugsCount()
        {
            return BuildCountLink("#Billy #Unresolved #Unassigned", "������������� ����");
        }

        [BlockModel(ContextKeys.TasksKey)]
        [BlockModelParameter("overallBillyBugsCount")]
        private BugsCountLinkInfoViewModel GetBillyOverallBugsCount()
        {
            return BuildCountLink("#Billy #Unresolved", "��� ���� �����");
        }

        [BlockModel(ContextKeys.TasksKey)]
        [BlockModelParameter("currentCSBugsCount")]
        private BugsCountLinkInfoViewModel GetCSCurrentBugsCount()
        {
            return BuildCountLink("#CS -Resolved", "��� ���� ��");
        }

        [BlockModel(ContextKeys.TasksKey)]
        [BlockModelParameter("billyNotVerified")]
        private BugsCountLinkInfoViewModel GetBillyNotVerifed()
        {
            return BuildCountLink("#Billy #Resolved -Verified -Obsolete Type: Bug, Task, Feature", "��������, �� �� ��������������");
        }

        private BugsCountLinkInfoViewModel BuildCountLink(string filterString, string description)
        {
            var count = bugTrackerClient.GetFilteredCount(filterString);
            return new BugsCountLinkInfoViewModel
                       {
                           Count = count,
                           Link = bugTrackerClient.GetBrowseFilterUrl(filterString),
                           Description = description
                       };
        }

        #endregion

        private CardListItemViewModel BuildCard(Dictionary<string, User> users, ILookup<string, BoardList> boardLists, 
                                                Dictionary<string, BoardSettings> boardSettings, ILookup<string, CardAction> cardActions,
                                                ILookup<string, CardChecklist> cardChecklists, BoardCard card, HashSet<string> rcBranches,
                                                BugsInfoViewModel bugs)
        {
            var stageInfo = cardStageInfoBuilder.Build(card, cardActions[card.Id].ToArray(),
                                                       cardChecklists[card.Id].ToArray(),
                                                       boardSettings[card.BoardId],
                                                       boardLists[card.BoardId].ToArray());
            
            var branchName = card.GetCardBranchName();
            var isInRc = !string.IsNullOrEmpty(branchName) && rcBranches.Contains(branchName);
            var analyticLink = card.GetAnalyticLink(wikiClient.GetBaseUrl(), bugTrackerClient.GetBaseUrl());

            return new CardListItemViewModel
                       {
                           CardId = card.Id,
                           CardName = card.Name,
                           AnalyticLink = analyticLink,
                           Labels = card.Labels.OrderBy(x => x.Color).ToArray(),
                           Avatars = card.UserIds.Select(id => users.SafeGet(id)).Where(x => x != null).Select(userAvatarViewModelBuilder.Build).ToArray(),
                           CardUrl = card.Url,
                           StageInfo = stageInfo,
                           IsNewCard = stageInfo.StageParrots.BeginDate.HasValue && stageInfo.StageParrots.BeginDate.Value.Date == DateTime.Now.Date,
                           BranchName = branchName,
                           IsInCandidateRelease = isInRc,
                           Bugs = bugs
                       };
        }
    }
}