using System;

namespace SKBKontur.Infrastructure.ContainerConfiguration
{
    public class ContainerGetException : Exception
    {
        public ContainerGetException(Type type, Exception innerException = null) 
            : base($"Can't get type {type.FullName} by container", innerException)
        {
        }
    }
}