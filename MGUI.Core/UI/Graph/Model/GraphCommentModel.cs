using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace MGUI.Core.UI.Graph
{
    public class GraphCommentModel
    {
        public Guid Id { get; set; }
        public Rectangle Bounds { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
        public Color? Color { get; set; }
        public Dictionary<string, string> EditorMetadata { get; set; } = new(StringComparer.Ordinal);

        public GraphCommentModel()
        {
        }

        public GraphCommentModel(Guid id, Rectangle bounds, string title, string text)
        {
            Id = id;
            Bounds = bounds;
            Title = title ?? string.Empty;
            Text = text ?? string.Empty;
        }
    }
}