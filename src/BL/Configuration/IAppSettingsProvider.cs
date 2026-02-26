using System;

namespace Zuto.Uk.Lenders.Common.Interfaces
{
    public interface IAppSettingsProvider
    {
        T Get<T>(string key) where T : IConvertible;
    }
}
