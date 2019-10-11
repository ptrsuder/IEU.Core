using System;
using Color = System.Drawing.Color;

namespace ImageEnhancingUtility
{
    public class LogMessage
    {
        public string Text { get; internal set; }
        public Color Color { get; internal set; }

        public LogMessage(string text, Color color)
        {
            Text = $"\n[{DateTime.Now}] {text}";
            Color = color;
        }
        public LogMessage(string text)
        {
            Text = $"\n[{DateTime.Now}] {text}";
            Color = Color.White;
        }

    }
}
