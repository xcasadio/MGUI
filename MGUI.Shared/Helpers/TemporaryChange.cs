using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Shared.Helpers
{
    /// <typeparam name="TItem">The type of item to modify</typeparam>
    public class TemporaryChange<TItem> : IDisposable
    {
        /// <summary>The value that <typeparamref name="TItem"/> will be reverted to when this object is disposed.</summary>
        public TItem Previous { get; }
        /// <summary>The value that <typeparamref name="TItem"/> is temporarily changed to.</summary>
        public TItem Temporary { get; }

        private Action<TItem> SetValue { get; }
        private Action<TItem> RevertValue { get; }

        /// <param name="PreviousValue">The value that <typeparamref name="TItem"/> will be reverted to when this object is disposed.</param>
        /// <param name="TemporaryValue">The value that <typeparamref name="TItem"/> will be temporarily changed to.</param>
        /// <param name="SetValue">An action to invoke that changes <typeparamref name="TItem"/> to the new, temporary value.</param>
        public TemporaryChange(TItem PreviousValue, TItem TemporaryValue, Action<TItem> SetValue)
            : this(PreviousValue, TemporaryValue, SetValue, SetValue) { }

        /// <param name="PreviousValue">The value that <typeparamref name="TItem"/> will be reverted to when this object is disposed.</param>
        /// <param name="TemporaryValue">The value that <typeparamref name="TItem"/> will be temporarily changed to.</param>
        /// <param name="SetValue">An action to invoke that changes <typeparamref name="TItem"/> to the new, temporary value.</param>
        /// <param name="RevertValue">An action to invoke that changes <typeparamref name="TItem"/> back to its previous value.</param>
        public TemporaryChange(TItem PreviousValue, TItem TemporaryValue, Action<TItem> SetValue, Action<TItem> RevertValue)
        {
            Previous = PreviousValue;
            Temporary = TemporaryValue;
            this.SetValue = SetValue;
            this.RevertValue = RevertValue;
            SetValue(Temporary);
        }

        public void Dispose()
        {
            RevertValue(Previous);
        }
    }
}
