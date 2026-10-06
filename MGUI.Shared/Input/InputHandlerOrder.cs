using System;
using System.Collections.Generic;

namespace MGUI.Shared.Input
{
    /// <summary>Orders the auto-updated handlers of an input tracker for its <c>UpdateHandlers</c> walk.</summary>
    internal static class InputHandlerOrder
    {
        /// <summary>Returns, in a new array, the handlers of <paramref name="Handlers"/> whose priority is not null, by descending priority, then in
        /// their order in <paramref name="Handlers"/>: the order that LINQ's stable <c>OrderByDescending</c> followed by <c>GroupBy</c> gave.
        /// Because the array is new, a walk over a previously returned array is not affected by later additions or removals.</summary>
        public static T[] Build<T>(List<T> Handlers, Func<T, double?> GetPriority)
        {
            var Count = 0;
            for (var i = 0; i < Handlers.Count; i++)
            {
                if (GetPriority(Handlers[i]).HasValue)
                {
                    Count++;
                }
            }

            var Ordered = new T[Count];
            var Filled = 0;
            for (var i = 0; i < Handlers.Count; i++)
            {
                var Handler = Handlers[i];
                var Priority = GetPriority(Handler);
                if (!Priority.HasValue)
                {
                    continue;
                }

                //  Stable insertion: only move past handlers of a strictly lower priority (double.CompareTo orders NaN lowest, as LINQ does).
                var Index = Filled;
                while (Index > 0 && GetPriority(Ordered[Index - 1]).Value.CompareTo(Priority.Value) < 0)
                {
                    Ordered[Index] = Ordered[Index - 1];
                    Index--;
                }

                Ordered[Index] = Handler;
                Filled++;
            }

            return Ordered;
        }
    }
}
