using System;
using System.ComponentModel;
using System.IO;
using DynamicData;
using ReactiveUI;
using Color = System.Drawing.Color;

namespace ImageEnhancingUtility.Core
{
    public class Logger : ReactiveObject
    {
        private string logs;
        [Browsable(false)]
        public string Logs
        {
            get => logs;
            set => this.RaiseAndSetIfChanged(ref logs, value);
        }

        public SourceList<LogMessage> Log = new SourceList<LogMessage>();

        bool _debugMode = false;
        public bool DebugMode
        {
            get => _debugMode;
            set => this.RaiseAndSetIfChanged(ref _debugMode, value);
        }

        public void Write(string text)
        {
            Write(text, Color.White);
        }

        public void WriteDebug(string text)
        {
            if (DebugMode)
                Write(text, Color.FromArgb(225, 0, 130));
        }

        public void Write(string text, Color color)
        {
            Write(new LogMessage(text, color));
        }

        public void Write(LogMessage message)
        {
            Log.Add(message);
            Logs += message.Text;
        }

        public void WriteOpenError(FileInfo file, string exMessage)
        {
            Write($"{exMessage}", Color.Red);
            Write($"Skipping <{file.Name}>...", Color.Red);
        }

    }

}
