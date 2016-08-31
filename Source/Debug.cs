using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Verse;

namespace Repair
{
    internal static class Debug<T>
    {
        internal static void PrintList(string title, IEnumerable<T> list)
        {
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
