﻿using System;
using SKBKontur.Infrastructure.Sugar;

namespace SKBKontur.Treller.WebApplication.Implementation.Services.Operationals.Operations
{
    public interface IRegularOperation
    {
        string Name { get; }
        OperationState State { get; }
        TimeSpan RunPeriod { get; }

        Maybe<Exception> Run();
    }
}