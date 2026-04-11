using MGUI.Shared.Helpers;
using MGUI.Backend.MonoGame;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Shared.Rendering
{
    internal abstract class View : ViewModelBase
    {
        protected IMonoGameDesktopBackend Backend { get; }
        public ObservableCollection<View> Children { get; }

        private bool _IsVisible;
        public bool IsVisible
        {
            get => _IsVisible;
            set
            {
                if (IsVisible != value)
                {
                    _IsVisible = value;
                    AutoNPC();
                }
            }
        }

        protected Rectangle _ScreenViewport;
        /// <summary>The entire screen bound's that this <see cref="View"/> occupies.</summary>
        public Rectangle ScreenViewport { get => _ScreenViewport; }

        /// <param name="NotifyChanged">True if <see cref="OnScreenViewportChanged"/> should be invoked.</param>
        /// <param name="BeforeNotify">Optional, can be null. If not null, this action will be invoked after <see cref="ScreenViewport"/> is set to the new <paramref name="Value"/>, but before <see cref="OnScreenViewportChanged"/> is invoked.</param>
        protected void SetScreenViewportBase(Rectangle Value, Action BeforeNotify, bool NotifyChanged = true)
        {
            if (ScreenViewport != Value)
            {
                Rectangle Previous = ScreenViewport;
                _ScreenViewport = Value;
                NPC(nameof(ScreenViewport));
                BeforeNotify?.Invoke();
                if (NotifyChanged)
                {
                    OnScreenViewportChanged?.Invoke(this, new(Previous, ScreenViewport));
                }
            }
        }

        public event EventHandler<EventArgs<Rectangle>> OnScreenViewportChanged;

        private enum TraversalType
        {
            Preorder,
            PostOrder
        }

        private IEnumerable<View> RecurseChildren(TraversalType Mode, bool IncludeSelf, bool IncludeHidden)
        {
            switch (Mode)
            {
                case TraversalType.Preorder:
                    if (IncludeSelf)
                    {
                        yield return this;
                    }

                    foreach (View Child in Children)
                    {
                        IEnumerable<View> RecursiveChildren = Child.RecurseChildren(Mode, true, IncludeHidden);
                        foreach (View Item in RecursiveChildren)
                        {
                            yield return Item;
                        }
                    }
                    break;
                case TraversalType.PostOrder:
                    foreach (View Child in Children)
                    {
                        IEnumerable<View> RecursiveChildren = Child.RecurseChildren(Mode, true, IncludeHidden);
                        foreach (View Item in RecursiveChildren)
                        {
                            yield return Item;
                        }
                    }
                    if (IncludeSelf)
                    {
                        yield return this;
                    }

                    break;
                default:
                    throw new NotImplementedException($"Unrecognized {nameof(TraversalType)}: {Mode}");
            }
        }

        protected View(IMonoGameDesktopBackend Backend, int ScreenViewportMargin)
            : this(Backend, Backend.Surface.GetBounds().GetCompressed(ScreenViewportMargin)) { }

        protected View(IMonoGameDesktopBackend Backend, Rectangle ScreenViewport)
        {
            this.Backend = Backend ?? throw new ArgumentNullException(nameof(Backend));
            IsVisible = true;
            SetScreenViewportBase(ScreenViewport, null, true);
            Children = new();
        }

        /// <summary>Invoked at the beginning of the <see cref="Update(UpdateBaseArgs)"/> method.</summary>
        public event EventHandler<UpdateBaseEventArgs> OnBeginUpdate;
        /// <summary>Invoked at the end of the <see cref="Update(UpdateBaseArgs)"/> method.</summary>
        public event EventHandler<UpdateBaseEventArgs> OnEndUpdate;

        public void Update(UpdateBaseArgs BA)
        {
            OnBeginUpdate?.Invoke(this, new(BA));
            foreach (View Child in Children)
            {
                Child.Update(BA);
            }

            UpdateSelf(BA);
            OnEndUpdate?.Invoke(this, new(BA));
        }

        protected abstract void UpdateSelf(UpdateBaseArgs BA);

        public void Draw(DrawBaseArgs BA)
        {
            if (IsVisible)
            {
                DrawBackground(BA);
                foreach (View Child in Children)
                {
                    Child.Draw(BA);
                }

                DrawForeground(BA);
            }
        }

        protected abstract void DrawBackground(DrawBaseArgs BA);
        protected abstract void DrawForeground(DrawBaseArgs BA);
    }
}
