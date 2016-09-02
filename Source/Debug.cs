using System;
using System.Collections.Generic;
using Verse;

namespace Repair
{
    internal static class Debug
    {
        private static readonly bool Enabled = false;

        internal static void PrintLine(string line)
        {
            if (!Enabled)
                return;

            Log.Message(line);
        }

        internal static void PrintList<T>(string title, IEnumerable<T> list)
        {
            if (!Enabled)
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
