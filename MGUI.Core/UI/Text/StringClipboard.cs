using System;
using System.Threading;

namespace MGUI.Core.UI.Text
{
    public class StringClipboard
    {
#if WINDOWS
        public string Text
        {
            get => ExecuteOnStaThread(() => System.Windows.Clipboard.GetText(System.Windows.TextDataFormat.Text) ?? string.Empty);
            set => ExecuteOnStaThread(() => System.Windows.Clipboard.SetText(value ?? string.Empty));
        }

        private static T ExecuteOnStaThread<T>(Func<T> operation)
        {
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                return operation();
            }

            T result = default;
            Exception failure = null;
            Thread thread = new(() =>
            {
                try
                {
                    result = operation();
                }
                catch (Exception ex)
                {
                    failure = ex;
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();

            if (failure != null)
            {
                throw failure;
            }

            return result;
        }

        private static void ExecuteOnStaThread(Action operation)
        {
            ExecuteOnStaThread(() =>
            {
                operation();
                return true;
            });
        }
#else
        public string Text { get; set; }
#endif

        public StringClipboard()
        {

        }
    }
}
