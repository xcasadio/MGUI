using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MGUI.Shared.Rendering
{
    public enum RasterizerType
    {
        Default,
        SolidScissorTest,
        Solid,
        WireframeScissorTest,
        Wireframe
    }

    public enum BlendType
    {
        /// <summary><see cref="BlendState.AlphaBlend"/></summary>
        Default,
        /// <summary><see cref="BlendState.Additive"/></summary>
        Additive,
        /// <summary><see cref="BlendState.AlphaBlend"/></summary>
        AlphaBlend,
        /// <summary><see cref="BlendState.NonPremultiplied"/></summary>
        NonPremultiplied,
        /// <summary><see cref="BlendState.Opaque"/></summary>
        Opaque,
        /// <summary>Disables color writes while allowing depth/stencil side effects.</summary>
        ColorWriteDisable,
        /// <summary>Uses the destination alpha channel as a mask while preserving ordinary alpha compositing.</summary>
        DestinationAlphaMask
    }

    public enum SamplerType
    {
        /// <summary><see cref="SamplerState.LinearClamp"/></summary>
        Default,
        /// <summary><see cref="SamplerState.AnisotropicClamp"/></summary>
        AnisotropicClamp,
        /// <summary><see cref="SamplerState.AnisotropicWrap"/></summary>
        AnisotropicWrap,
        /// <summary><see cref="SamplerState.LinearClamp"/></summary>
        LinearClamp,
        /// <summary><see cref="SamplerState.LinearWrap"/></summary>
        LinearWrap,
        /// <summary><see cref="SamplerState.PointClamp"/></summary>
        PointClamp,
        /// <summary><see cref="SamplerState.PointWrap"/></summary>
        PointWrap
    }

    public enum DepthStencilType
    {
        /// <summary><see cref="DepthStencilState.None"/></summary>
        Default,
        /// <summary><see cref="DepthStencilState.DepthRead"/></summary>
        DepthRead,
        /// <summary><see cref="DepthStencilState.None"/></summary>
        None,
        /// <summary>Writes to stencil by incrementing values that pass the current stencil test.</summary>
        StencilWriteIncrement,
        /// <summary>Reads only pixels whose stencil value equals the supplied reference.</summary>
        StencilReadEqual,
        /// <summary>Restores stencil by decrementing values that equal the supplied reference.</summary>
        StencilRestoreDecrement
    }

    public enum DrawSortMode
    {
        Deferred,
        Immediate,
        Texture,
        BackToFront,
        FrontToBack
    }

    /// <summary>Stores backend-neutral draw settings that a concrete renderer maps to its own batching and GPU state model.</summary>
    public record class DrawSettings(Matrix Transform, RasterizerType RasterizerType = RasterizerType.SolidScissorTest, DrawSortMode Sort = DrawSortMode.Deferred,
        BlendType BlendType = BlendType.AlphaBlend, SamplerType SamplerType = SamplerType.PointClamp, DepthStencilType DepthStencilType = DepthStencilType.None,
        object BackendEffect = null, int StencilReference = 0, int StencilReadMask = 0xFF, int StencilWriteMask = 0xFF)
    {
        private Matrix? _InverseTransform;
        public Matrix InverseTransform
        {
            get
            {
                if (!_InverseTransform.HasValue)
                {
                    _InverseTransform = IsIdentityTransform ? Matrix.Identity : Matrix.Invert(Transform);
                }
                return _InverseTransform.Value;
            }
        }

        public bool IsIdentityTransform { get; } = Transform == Matrix.Identity;
        public bool UsesScissorTest => RasterizerType == RasterizerType.SolidScissorTest || RasterizerType == RasterizerType.WireframeScissorTest;

        public static DrawSettings Default => new(Matrix.Identity);
    }
}
