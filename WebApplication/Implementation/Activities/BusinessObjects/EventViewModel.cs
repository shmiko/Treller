﻿using System;
using System.Collections.Generic;

namespace SKBKontur.Treller.WebApplication.Implementation.Activities.BusinessObjects
{
    public class EventViewModel
    {
        public string EventName { get; set; }
        public DateTime EventStartDate { get; set; }
        public KeyValuePair<DateTime, Activity[]>[] ActivitiesByDate { get; set; }
    }
}