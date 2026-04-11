using Microsoft.Xna.Framework;
using MGUI.Shared.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MonoGame.Extended;
using MGUI.Core.UI.Containers;
using MGUI.Core.UI.Brushes.Border_Brushes;
using MGUI.Shared.Rendering;
using System.Diagnostics;

namespace MGUI.Core.UI
{
    public class MGCheckBox : MGSingleContentHost
    {
        internal static bool? GetNextCheckedState(bool? isChecked, bool isThreeState)
        {
            if (isThreeState)
            {
                return isChecked.HasValue && isChecked.Value ? null : !isChecked.HasValue ? false : true;
            }

            return !isChecked.HasValue || isChecked.Value ? false : true;
        }

        /// <summary>The default width/height of the checkable part of an <see cref="MGCheckBox"/></summary>
        public const int DefaultCheckBoxSize = 16;
        /// <summary>The default empty width between the checkable part of an <see cref="MGCheckBox"/> and its <see cref="MGSingleContentHost.Content"/></summary>
        public const int DefaultCheckBoxSpacingWidth = 5;

        /// <summary>Provides direct access to the button component that appears to the left of this checkbox's content.<para/>
        /// See also: <see cref="ButtonElement"/></summary>
        public MGComponent<MGButton> ButtonComponent { get; }
        /// <summary>The checkable button portion of this <see cref="MGCheckBox"/></summary>
        public MGButton ButtonElement { get; }
        private MGComponent<MGCheckStateIcon> CheckStateIconComponent { get; }
        private MGCheckStateIcon CheckStateIcon { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private int _CheckBoxComponentSize;
        /// <summary>The dimensions of the checkable part of this <see cref="MGCheckBox"/>.<para/>
        /// See also: <see cref="DefaultCheckBoxSize"/></summary>
        public int CheckBoxComponentSize
        {
            get => _CheckBoxComponentSize;
            set
            {
                if (_CheckBoxComponentSize != value)
                {
                    _CheckBoxComponentSize = value;

                    Size ButtonSize = new(CheckBoxComponentSize, CheckBoxComponentSize);
                    ButtonElement.PreferredWidth = ButtonSize.Width;
                    ButtonElement.PreferredHeight = ButtonSize.Height;

                    NPC(nameof(CheckBoxComponentSize));
                }
            }
        }

        /// <summary>The reserved empty width between the checkable part of this <see cref="MGCheckBox"/> and its <see cref="MGSingleContentHost.Content"/>.<para/>
        /// See also: <see cref="DefaultCheckBoxSpacingWidth"/>.<para/>
        /// This value is functionally equivalent to <see cref="ButtonElement"/>'s right <see cref="MGElement.Margin"/></summary>
        public int SpacingWidth
        {
            get => ButtonElement.Margin.Right;
            set
            {
                if (ButtonElement.Margin.Right != value)
                {
                    ButtonElement.Margin = ButtonElement.Margin.ChangeRight(value);
                    NPC(nameof(SpacingWidth));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Color _CheckMarkColor;
        /// <summary>The <see cref="Color"/> to use when stroking the check mark if <see cref="IsChecked"/> is true.<para/>
        /// Default value: <see cref="MGTheme.CheckMarkColor"/><para/>
        /// See also:<br/><see cref="MGWindow.Theme"/><br/><see cref="MGDesktop.Theme"/></summary>
        public Color CheckMarkColor
        {
            get => _CheckMarkColor;
            set
            {
                if (_CheckMarkColor != value)
                {
                    _CheckMarkColor = value;
                    if (CheckStateIcon != null)
                    {
                        CheckStateIcon.MarkColor = value;
                    }
                    NPC(nameof(CheckMarkColor));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsCheckMarkShadowed;
        /// <summary>If true, the graphics of the <see cref="ButtonElement"/> will be drawn an extra time
        /// with Color=<see cref="CheckMarkShadowColor"/> and using Offset=<see cref="CheckMarkShadowOffset"/><para/>
        /// Default value: false</summary>
        public bool IsCheckMarkShadowed
        {
            get => _IsCheckMarkShadowed;
            set
            {
                if (_IsCheckMarkShadowed != value)
                {
                    _IsCheckMarkShadowed = value;
                    if (CheckStateIcon != null)
                    {
                        CheckStateIcon.IsShadowed = value;
                    }
                    NPC(nameof(IsCheckMarkShadowed));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Color _CheckMarkShadowColor;
        public Color CheckMarkShadowColor
        {
            get => _CheckMarkShadowColor;
            set
            {
                if (_CheckMarkShadowColor != value)
                {
                    _CheckMarkShadowColor = value;
                    if (CheckStateIcon != null)
                    {
                        CheckStateIcon.ShadowColor = value;
                    }
                    NPC(nameof(CheckMarkShadowColor));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Point _CheckMarkShadowOffset;
        public Point CheckMarkShadowOffset
        {
            get => _CheckMarkShadowOffset;
            set
            {
                if (_CheckMarkShadowOffset != value)
                {
                    _CheckMarkShadowOffset = value;
                    if (CheckStateIcon != null)
                    {
                        CheckStateIcon.ShadowOffset = value;
                    }
                    NPC(nameof(CheckMarkShadowOffset));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsThreeState;
        /// <summary>True if 'null' is a valid value for <see cref="IsChecked"/><para/>
        /// Default value: false</summary>
        public bool IsThreeState
        {
            get => _IsThreeState;
            set
            {
                if (_IsThreeState != value)
                {
                    _IsThreeState = value;
                    if (!IsThreeState && !IsChecked.HasValue)
                    {
                        IsChecked = false;
                    }

                    NPC(nameof(IsThreeState));
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool? _IsChecked;
        /// <summary>If <see cref="IsThreeState"/> is false, this value should not be set to null.</summary>
        public bool? IsChecked
        {
            get => _IsChecked;
            set
            {
                if (_IsChecked != value)
                {
                    if (!IsThreeState && !value.HasValue)
                    {
                        throw new InvalidOperationException($"{nameof(MGCheckBox)}.{nameof(IsChecked)} can only be set to 'null' if {nameof(IsThreeState)} is true.");
                    }

                    bool? Previous = IsChecked;
                    _IsChecked = value;
                    if (CheckStateIcon != null)
                    {
                        CheckStateIcon.CheckState = value;
                    }
                    NPC(nameof(IsChecked));
                    OnCheckStateChanged?.Invoke(this, new(Previous, IsChecked));

                    if (IsChecked.HasValue)
                    {
                        if (IsChecked.Value)
                        {
                            OnChecked?.Invoke(this, EventArgs.Empty);
                        }
                        else
                        {
                            OnUnchecked?.Invoke(this, EventArgs.Empty);
                        }
                    }
                }
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private bool _IsReadonly;
        /// <summary>If true, the user will be unable to modify <see cref="IsChecked"/> by manually clicking the <see cref="ButtonElement"/>.<para/>
        /// Default value: false</summary>
        public bool IsReadonly
        {
            get => _IsReadonly;
            set
            {
                if (_IsReadonly != value)
                {
                    _IsReadonly = value;
                    NPC(nameof(IsReadonly));
                }
            }
        }

        /// <summary>Note: This event is invoked before <see cref="OnChecked"/> / <see cref="OnUnchecked"/></summary>
        public event EventHandler<EventArgs<bool?>> OnCheckStateChanged;
        public event EventHandler<EventArgs> OnChecked;
        public event EventHandler<EventArgs> OnUnchecked;

        public MGCheckBox(MGWindow Window, bool? IsChecked = false)
            : base(Window, MGElementType.CheckBox)
        {
            using (BeginInitializing())
            {
                IsFocusable = true;
                ButtonElement = new(Window, new(1), MGUniformBorderBrush.Black, x =>
                {
                    if (!IsReadonly)
                    {
                        this.IsChecked = GetNextCheckedState(this.IsChecked, IsThreeState);
                    }
                });
                ButtonElement.IsFocusable = false;
                ButtonElement.MinWidth = 12;
                ButtonElement.MinHeight = 12;
                ButtonElement.Padding = new(0);

                CheckStateIcon = new(Window) { ManagedParent = this };

                ButtonComponent = new(ButtonElement, false, true, true, true, false, false, false,
                    (AvailableBounds, ComponentSize) => ApplyAlignment(AvailableBounds, HorizontalAlignment.Left, VerticalAlignment.Center, ComponentSize.Size));
                CheckStateIconComponent = new(CheckStateIcon, false, true, true, true, false, false, false,
                    (AvailableBounds, ComponentSize) => ButtonElement.LayoutBounds.GetCompressed(ButtonElement.BorderThickness));

                AddComponent(ButtonComponent);
                AddComponent(CheckStateIconComponent);

                CheckBoxComponentSize = DefaultCheckBoxSize;
                SpacingWidth = DefaultCheckBoxSpacingWidth;
                CheckMarkColor = GetTheme().CheckMarkColor;
                IsCheckMarkShadowed = false;
                CheckMarkShadowColor = Color.Black;
                CheckMarkShadowOffset = new(0, 1);

                IsThreeState = !IsChecked.HasValue;
                this.IsChecked = IsChecked;
                IsReadonly = false;
            }
        }

        public override bool TryHandleNavigationAction(UINavigationAction action)
        {
            if (action != UINavigationAction.Submit || IsReadonly)
            {
                return false;
            }

            IsChecked = GetNextCheckedState(IsChecked, IsThreeState);
            return true;
        }

        public static void DrawCheckMark(MGDesktop desktop, Rectangle bounds, IUIDrawContext drawContext, float opacity, Point offset, Color color)
        {
            UISymbolDrawing.DrawCheckMark(drawContext, offset.ToVector2(), bounds, color * opacity);
        }

        public override void DrawSelf(ElementDrawArgs DA, Rectangle LayoutBounds)
        {

        }
    }
}
