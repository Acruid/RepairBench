using System;
using System.Collections.Generic;
using Verse;

namespace Repair
{
    internal static class Debug
    {
        internal static void PrintLine(string line)
        {
            if (!Settings.DebugEnabled)
                return;

            Log.Message(line);
        }

        internal static void PrintList<T>(string title, IEnumerable<T> list)
        {
            if (!Settings.DebugEnabled)
                return;

            Log.Message("-- DebugList - "+ title + " --");
            try
            {
                foreach (var ingred in list)
                {
                    Log.Message("    " + ingred);
                }
            }
            catch (Exception e)
            {
                Log.Message("Error printing list: " + e.Message);
            }
            finally
            {
                Log.Message("---------------");
            }
        }
    }
}
