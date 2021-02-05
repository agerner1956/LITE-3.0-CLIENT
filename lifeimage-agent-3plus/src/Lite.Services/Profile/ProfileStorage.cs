using Lite.Core;
using Lite.Core.Guard;
using Lite.Core.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Lite.Services
{
    public sealed class ProfileStorage : IProfileStorage
    {
        public const string CacheKey = "current_profile";

        private readonly IMemoryCache _cache;
        private readonly LiteConfig _liteConfig;
        public ProfileStorage(IMemoryCache cache, ILiteConfigService liteConfigService)
        {
            _cache = cache;
            _liteConfig = liteConfigService.GetDefaults();
        }        
        public Profile Current
        {
            get
            {
                return _cache.Get<Profile>(CacheKey);                
            }
        }

        public void Set(Profile profile)
        {
            Throw.IfNull(profile);
            if (profile.tempPath == null)
            {
                profile.tempPath = _liteConfig.TempPath;
            }
            _cache.Set(CacheKey, profile);
        }
    }
}
