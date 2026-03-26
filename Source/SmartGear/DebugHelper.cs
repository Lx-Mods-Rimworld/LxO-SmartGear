using System.Linq;
using Verse;

namespace SmartGear
{
    public static class SGDebug
    {
        private static bool? _debugActive;
        public static bool IsActive
        {
            get
            {
                if (!_debugActive.HasValue)
                    _debugActive = ModsConfig.ActiveModsInLoadOrder.Any(
                        m => m.PackageIdPlayerFacing != null
                        && m.PackageIdPlayerFacing.ToLower().Contains("lexxers.debug"));
                return _debugActive.Value;
            }
        }
        public static void Log(string message)
        {
            if (IsActive) Verse.Log.Message(message);
        }
    }
}
