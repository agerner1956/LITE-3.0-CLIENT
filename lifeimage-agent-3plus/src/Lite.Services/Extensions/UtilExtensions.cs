using Lite.Core.Utils;
using System.Collections.Generic;

namespace Lite.Services
{
    public static class UtilExtensions
    {
        public static List<string> DirSearch(this IUtil util, string dir, string pattern)
        {
            return util.DiskUtils.DirSearch(dir, pattern);
        }
    }
}
