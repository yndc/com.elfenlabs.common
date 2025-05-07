using System.Diagnostics;

namespace Elfenlabs.Debug
{
    public static class Log
    {
        [Conditional("UNITY_DEBUG_VERBOSE")]
        public static void Verbose(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        public static void Info(string message)
        {
            UnityEngine.Debug.Log(message);
        }

        public static void Error(string message)
        {
            UnityEngine.Debug.LogError(message);
        }
    }
}