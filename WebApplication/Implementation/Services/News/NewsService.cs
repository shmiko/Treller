﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.Ajax.Utilities;
using SKBKontur.BlocksMapping.BlockExtenssions;
using SKBKontur.Infrastructure.CommonExtenssions;
using SKBKontur.TaskManagerClient;
using SKBKontur.TaskManagerClient.Notifications;
using SKBKontur.Treller.WebApplication.Implementation.Infrastructure.Storages;
using SKBKontur.Treller.WebApplication.Implementation.Services.Settings;
using SKBKontur.Treller.WebApplication.Implementation.Services.TaskCacher;
using SKBKontur.Treller.WebApplication.Implementation.Services.TaskManager;
using SKBKontur.Treller.WebApplication.Implementation.TaskDetalization.BusinessObjects.Models;

namespace SKBKontur.Treller.WebApplication.Implementation.Services.News
{
    public class NewsService : INewsService
    {
        private const string CardNewsName = "CardNews";

        #region init

        private readonly ICachedFileStorage cachedFileStorage;
        private readonly IKanbanBoardMetaInfoBuilder kanbanBoardMetaInfoBuilder;
        private readonly ITaskCacher taskCacher;
        private readonly ITaskManagerClient taskManagerClient;
        private readonly ICardStateInfoBuilder cardStateInfoBuilder;
        private readonly INotificationService notificationService;
        private readonly INewsSettingsService newsSettingsService;

        public NewsService(ICachedFileStorage cachedFileStorage,
                           IKanbanBoardMetaInfoBuilder kanbanBoardMetaInfoBuilder,
                           ITaskCacher taskCacher,
                           ITaskManagerClient taskManagerClient,
                           ICardStateInfoBuilder cardStateInfoBuilder,
                           INotificationService notificationService,
                           INewsSettingsService newsSettingsService)
        {
            this.cachedFileStorage = cachedFileStorage;
            this.kanbanBoardMetaInfoBuilder = kanbanBoardMetaInfoBuilder;
            this.taskCacher = taskCacher;
            this.taskManagerClient = taskManagerClient;
            this.cardStateInfoBuilder = cardStateInfoBuilder;
            this.notificationService = notificationService;
            this.newsSettingsService = newsSettingsService;
        }
        #endregion

        public void Refresh()
        {
            var kanbanBoardsMetaInfos = kanbanBoardMetaInfoBuilder.BuildForAllOpenBoards();
            var boardIds = kanbanBoardsMetaInfos.Select(x => x.Id).ToArray();
            var cards = taskCacher.GetCached(boardIds, strings => taskManagerClient.GetBoardCardsAsync(strings).Result, TaskCacherStoredTypes.BoardCards);
            var boardLists = taskCacher.GetCached(boardIds, ids => taskManagerClient.GetBoardListsAsync(ids).Result, TaskCacherStoredTypes.BoardLists).ToLookup(x => x.BoardId);
            var cardActions = taskCacher.GetCached(boardIds, strings => taskManagerClient.GetActionsForBoardCardsAsync(strings).Result, TaskCacherStoredTypes.BoardActions).ToLookup(x => x.CardId);

            var actualCards = cards
                .Where(x => !x.Name.Contains("Автотесты", StringComparison.OrdinalIgnoreCase) && x.LastActivity.Date > DateTime.Now.Date.AddDays(-30))
                .Select(card =>
                {
                    var cardStateInfo = cardStateInfoBuilder.Build(cardActions[card.Id].ToArray(), kanbanBoardsMetaInfos.ToDictionary(x => x.Id), boardLists.ToDictionary(x => x.Key, x => x.ToArray()));
                    var cardReleaseDate = (card.DueDate ?? cardStateInfo.States.SafeGet(CardState.Released).IfNotNull(x => (DateTime?)x.BeginDate) ?? DateTime.Now).Date;
                    return new CardNewsModel
                    {
                        CardId = card.Id,
                        CardName = card.Name,
                        Labels = card.Labels.OrderBy(x => x.Color).ToArray(),
                        CardDescription = card.Description,
                        State = cardStateInfo.CurrentState,
                        DueDate = card.DueDate,
                        CardReleaseDate = cardReleaseDate,
                        PublishDate = DateTime.Now.Date
                    };
                })
                .Where(x => ((x.State == CardState.ReleaseWaiting || x.State == CardState.Testing) && x.DueDate.HasValue) || (x.State == CardState.Released))
                .Where(x => x.CardReleaseDate >= DateTime.Now.Date.AddDays(-14))
                .ToArray();

            var currentModels = (cachedFileStorage.Find<CardNewsModel[]>(CardNewsName) ?? new CardNewsModel[0]).ToDictionary(x => x.CardId);
            foreach (var card in actualCards.Where(card => currentModels.ContainsKey(card.CardId)))
            {
                var oldCard = currentModels[card.CardId];
                card.PublishDate = oldCard.PublishDate;
                card.IsDeleted = oldCard.IsDeleted;
                card.IsTechnicalNewsPublished = oldCard.IsTechnicalNewsPublished;
                card.IsNewsPublished = oldCard.IsNewsPublished;
            }
            cachedFileStorage.Write(CardNewsName, actualCards);
        }

        public NewsViewModel GetNews()
        {
            var cardsForNews = cachedFileStorage.Find<CardNewsModel[]>(CardNewsName) ?? new CardNewsModel[0];
            return new NewsViewModel
            {
                NewsToPublish = BuildNewsModel(cardsForNews, false),
                TechnicalNewsToPublish = BuildNewsModel(cardsForNews, true),
                NotActualCards = cardsForNews.Where(x => x.IsPublished() || x.IsDeleted).ToArray(),
                CardsWihoutNews = cardsForNews.Where(x => !x.IsPublished() && !x.IsDeleted && !x.IsNewsExists()).ToArray(),
                ActualCards = cardsForNews.Where(x => !x.IsPublished() && !x.IsDeleted && x.IsNewsExists()).ToArray()
            };
        }

        private static NewsModel BuildNewsModel(CardNewsModel[] cards, bool isTechnicalNews, bool inHtmlStyle = false)
        {
            var newLine = inHtmlStyle ? "<br/>" : Environment.NewLine;
            var news = new StringBuilder();
            DateTime releaseDate = new DateTime();
            var cardsForSend = new List<CardNewsModel>();

            cards = cards.Where(x => !x.IsPublished(isTechnicalNews) && !x.IsDeleted) // && x.PublishDate >= DateTime.Today
                         .OrderBy(x => x.CardReleaseDate)
                         .ToArray();

            foreach (var card in cards)
            {
                var newsText = isTechnicalNews ? card.TechnicalNewsText : card.NewsText;

                if (string.IsNullOrWhiteSpace(newsText))
                {
                    continue;
                }

                if (card.CardReleaseDate != releaseDate)
                {
                    news.AppendFormat("Вечером {1} {0}:", card.CardReleaseDate >= DateTime.Today ? "будем релизить" : "состоялся релиз", card.CardReleaseDate.ToString("D", new CultureInfo("ru-RU", false)));
                    news.Append(newLine);
                    news.Append(newLine);
                }
                releaseDate = card.CardReleaseDate;

                news.Append(inHtmlStyle ? "<b>" : "Задача: ");
                news.Append(card.CardName);
                news.Append(inHtmlStyle ? "</b>" : "");
                news.Append(newLine);
                news.Append(newsText);
                news.Append(newLine);
                news.Append(newLine);
                cardsForSend.Add(card);
            }

            if (news.Length == 0)
            {
                return null;
            }

            return new NewsModel
            {
                Cards = cardsForSend.ToArray(),
                NewsHeader = isTechnicalNews ? "Технические релизы Контур.Биллинг" : "Релизы Контур.Биллинг",
                NewsText = news.ToString()
            };
        }

        public void DeleteCard(string cardId)
        {
            UpdateCard(cardId, card => card.IsDeleted = true);
        }

        public void RestoreCard(string cardId)
        {
            UpdateCard(cardId, card =>
            {
                card.IsTechnicalNewsPublished = false;
                card.IsNewsPublished = false;
                card.IsDeleted = false;
                card.PublishDate = DateTime.Today;
            });
        }

        private void UpdateCard(string cardId, Action<CardNewsModel> updateCardAction)
        {
            var newsCards = cachedFileStorage.Find<CardNewsModel[]>(CardNewsName) ?? new CardNewsModel[0];
            var cardToDelete = newsCards.FirstOrDefault(x => x.CardId == cardId);

            if (cardToDelete == null)
            {
                return;
            }

            updateCardAction(cardToDelete);
            cachedFileStorage.Write(CardNewsName, newsCards);
        }

        public void SendTechnicalNews()
        {
            SendNews(true);
        }

        public void SendNews()
        {
            SendNews(false);
        }

        private void SendNews(bool technical)
        {
            var cards = cachedFileStorage.Find<CardNewsModel[]>(CardNewsName) ?? new CardNewsModel[0];
            var inHtmlStyle = true;
            var newsModel = BuildNewsModel(cards, technical, inHtmlStyle);
            if (newsModel == null || newsModel.Cards.Length == 0)
            {
                return;
            }

            var body = string.Format("{1}{0}{0}Вы можете ответить на это письмо, если у вас возникли вопросы или комментарии касающиеся релизов{0}{0}--{0}С уважением, команда Контур.Биллинг", inHtmlStyle ? "<br/>" : Environment.NewLine, newsModel.NewsText);
            var notification = new Notification
            {
                Title = newsModel.NewsHeader,
                Body = body,
                IsHtml = inHtmlStyle,
                Recipient = technical ? newsSettingsService.GetOrRead().TechMailingList : newsSettingsService.GetOrRead().PublicMailingList,
                ReplyTo = "ask.billing@skbkontur.ru"
            };
            notificationService.Send(notification);

            foreach (var card in newsModel.Cards)
            {
                card.Publish(technical);
            }
            
            cachedFileStorage.Write(CardNewsName, cards);
        }

        public bool IsAnyNewsExists()
        {
            return (cachedFileStorage.Find<CardNewsModel[]>(CardNewsName) ?? new CardNewsModel[0]).Any(x => x.IsNewsExists() && !x.IsPublished() && !x.IsDeleted);
        }
    }
}