using Microsoft.Xna.Framework.Input;
using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace MGUI.Shared.Input.Keyboard
{
    internal static class KeyboardInputProbe
    {
        private const string OutputPathEnvironmentVariable = "CASA_MGUI_INPUT_PROBE";

        private static readonly object SyncRoot = new();
        private static bool _initialized;
        private static string _outputPath;

        public static bool IsEnabled
        {
            get
            {
                EnsureInitialized();
                return _outputPath != null;
            }
        }

        public static void RecordTextInputQueued(char character, Keys key)
        {
            if (!IsEnabled)
            {
                return;
            }

            AppendLine($"[MGUI.Keyboard] native-text queued char={FormatCharacter(character)} key={key}");
        }

        public static void RecordKeyPressed(Keys key, string printableValue, string fallbackValue, bool usedNativeText)
        {
            if (!IsEnabled)
            {
                return;
            }

            AppendLine($"[MGUI.Keyboard] key-pressed key={key} printable={FormatString(printableValue)} fallback={FormatString(fallbackValue)} source={(usedNativeText ? "native-text" : "key-map")}");
        }

        public static void RecordHandlerGate(IKeyboardHandlerHost owner, string reason)
        {
            if (!IsEnabled)
            {
                return;
            }

            AppendLine($"[MGUI.Keyboard] handler-gate owner={DescribeOwner(owner)} reason={reason}");
        }

        public static void RecordPressedDelivery(IKeyboardHandlerHost owner, BaseKeyPressedEventArgs args, bool delivered, string reason)
        {
            if (!IsEnabled)
            {
                return;
            }

            AppendLine($"[MGUI.Keyboard] pressed-delivery owner={DescribeOwner(owner)} key={args.Key} printable={FormatString(args.PrintableValue)} delivered={delivered.ToString(CultureInfo.InvariantCulture)} reason={reason}");
        }

        private static void EnsureInitialized()
        {
            if (_initialized)
            {
                return;
            }

            lock (SyncRoot)
            {
                if (_initialized)
                {
                    return;
                }

                string rawPath = Environment.GetEnvironmentVariable(OutputPathEnvironmentVariable);
                if (!string.IsNullOrWhiteSpace(rawPath) && !string.Equals(rawPath, "0", StringComparison.OrdinalIgnoreCase))
                {
                    _outputPath = string.Equals(rawPath, "1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(rawPath, "true", StringComparison.OrdinalIgnoreCase)
                            ? Path.GetFullPath("mgui-keyboard-input-probe.txt")
                            : Path.GetFullPath(rawPath);

                    string directory = Path.GetDirectoryName(_outputPath);
                    if (!string.IsNullOrWhiteSpace(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    File.WriteAllText(_outputPath,
                        "CasaEngine MGUI keyboard input probe" + Environment.NewLine +
                        $"Started: {DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}" + Environment.NewLine + Environment.NewLine);
                }

                _initialized = true;
            }
        }

        private static void AppendLine(string line)
        {
            lock (SyncRoot)
            {
                File.AppendAllText(_outputPath, line + Environment.NewLine);
            }
        }

        private static string DescribeOwner(IKeyboardHandlerHost owner)
        {
            if (owner == null)
            {
                return "<null>";
            }

            return owner.GetType().Name;
        }

        private static string FormatString(string value)
        {
            if (value == null)
            {
                return "<null>";
            }

            if (value.Length == 0)
            {
                return "<empty>";
            }

            var builder = new StringBuilder(value.Length + 2);
            builder.Append('\'');
            for (int index = 0; index < value.Length; index++)
            {
                builder.Append(FormatCharacterCore(value[index]));
            }

            builder.Append('\'');
            return builder.ToString();
        }

        private static string FormatCharacter(char character)
            => '\'' + FormatCharacterCore(character) + '\'';

        private static string FormatCharacterCore(char character)
            => character switch
            {
                '\r' => "\\r",
                '\n' => "\\n",
                '\t' => "\\t",
                '\0' => "\\0",
                _ when char.IsControl(character) => "\\u" + ((int)character).ToString("X4", CultureInfo.InvariantCulture),
                _ => character.ToString(),
            };
    }
}