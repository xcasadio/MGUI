using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Shared.Helpers
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>One <see cref="PropertyChangedEventArgs"/> per property name, shared by every instance: a notification with a subscriber allocates nothing.</summary>
        private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, PropertyChangedEventArgs> CachedArgs = new();

        public virtual void NotifyPropertyChanged([CallerMemberName] string PropertyName = null)
        {
            PropertyChangedEventHandler Handler = PropertyChanged;
            if (Handler != null)
            {
                Handler(this, PropertyName == null ? new PropertyChangedEventArgs(null) : CachedArgs.GetOrAdd(PropertyName, static Name => new PropertyChangedEventArgs(Name)));
            }
        }
    }
}
