﻿using NUnit.Framework;

namespace SKBKontur.Treller.Tests.UnitWrappers
{
    public class SkipTestAttribute : IgnoreAttribute
    {
        public SkipTestAttribute(string reason) : base(reason)
        {
        }
    }
}