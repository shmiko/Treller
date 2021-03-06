﻿using System;
using SKBKontur.TaskManagerClient.BusinessObjects.TaskManager;
using SKBKontur.Treller.WebApplication.Implementation.TaskDetalization.BusinessObjects.Models;

namespace SKBKontur.Treller.WebApplication.Implementation.Services.News
{
    public class CardNewsModel
    {
        private string cardDescription;

        public string CardId { get; set; }
        public string CardName { get; set; }

        public string CardDescription
        {
            get { return cardDescription; }
            set
            {
                cardDescription = value;
                NewsText = BuildCardNews(false);
                TechnicalNewsText = BuildCardNews(true);
            }
        }
        public CardLabel[] Labels { get; set; }
        public CardState State { get; set; }
        public DateTime? DueDate { get; set; }
        public DateTime CardReleaseDate { get; set; }
        public DateTime PublishDate { get; set; }
        public bool IsNewsPublished { get; set; }
        public bool IsTechnicalNewsPublished { get; set; }
        public bool IsDeleted { get; set; }
        public string NewsText { get; private set; }
        public string TechnicalNewsText { get; private set; }

        private string BuildCardNews(bool isTechnicalNews)
        {
            var marker = isTechnicalNews ? "**Технические новости**" : "**Новости**";
            var isPublished = (isTechnicalNews && IsTechnicalNewsPublished) || (!isTechnicalNews && IsNewsPublished);

            if (IsDeleted || isPublished || string.IsNullOrWhiteSpace(CardDescription))
            {
                return null;
            }

            var newsIndex = CardDescription.IndexOf(marker, 0, StringComparison.OrdinalIgnoreCase);
            if (newsIndex < 0)
            {
                return null;
            }

            newsIndex += marker.Length + 1;

            if (newsIndex >= CardDescription.Length)
            {
                return null;
            }

            var postNewsIndex = CardDescription.IndexOf("**", newsIndex, StringComparison.OrdinalIgnoreCase);
            var newsLength = (postNewsIndex < 0 ? CardDescription.Length - 1 : postNewsIndex) - newsIndex;

            if (newsLength <= 0)
            {
                return null;
            }

            var cardNews = CardDescription.Substring(newsIndex, newsLength);
            if (string.IsNullOrWhiteSpace(cardNews) || cardNews.Length < 10)
            {
                return null;
            }

            return cardNews.Trim();
        }
        public bool IsNewsExists()
        {
            return !string.IsNullOrWhiteSpace(NewsText) || !string.IsNullOrWhiteSpace(TechnicalNewsText);
        }

        public bool IsPublished()
        {
            if (!string.IsNullOrWhiteSpace(NewsText) && !IsNewsPublished)
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(TechnicalNewsText) && !IsTechnicalNewsPublished)
            {
                return false;
            }

            return true;
        }

        public bool IsPublished(bool technicalNews)
        {
            return technicalNews ? IsTechnicalNewsPublished : IsNewsPublished;
        }

        public void MarkPublished(bool technicalNews)
        {
            if (technicalNews)
                IsTechnicalNewsPublished = true;
            else
                IsNewsPublished = true;
        }

        public void MarkDeleted()
        {
            IsDeleted = true;
        }

        public void ResetState(DateTime publishDate)
        {
            IsTechnicalNewsPublished = false;
            IsNewsPublished = false;
            IsDeleted = false;
            PublishDate = publishDate;
        }
    }
}