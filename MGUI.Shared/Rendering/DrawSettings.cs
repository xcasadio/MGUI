using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
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
        Opaque
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

    /// <summary>Stores settings used by <see cref="SpriteBatch.Begin(SpriteSortMode, BlendState, SamplerState, DepthStencilState, RasterizerState, Effect, Matrix?)"/></summary>
    public record class DrawSettings(Matrix Transform, RasterizerType RasterizerType = RasterizerType.SolidScissorTest, SpriteSortMode Sort = SpriteSortMode.Deferred,
        BlendType BlendType = BlendType.AlphaBlend, SamplerType SamplerType = SamplerType.PointClamp, DepthStencilType DepthStencilType = DepthStencilType.None,
        Effect Effect = null, int StencilReference = 0, int StencilReadMask = 0xFF, int StencilWriteMask = 0xFF)
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

        public static DrawSettings Default => new(Matrix.Identity);

        private static readonly Dictionary<RasterizerType, RasterizerState> RasterizerMap = new()
        {
            { RasterizerType.Default, new() { CullMode = CullMode.None } },
            { RasterizerType.SolidScissorTest, new() { FillMode = FillMode.Solid, ScissorTestEnable = true, CullMode = CullMode.None } },
            { RasterizerType.Solid, new() { FillMode = FillMode.Solid, ScissorTestEnable = false, CullMode = CullMode.None } },
            { RasterizerType.WireframeScissorTest, new() { FillMode = FillMode.WireFrame, ScissorTestEnable = true, CullMode = CullMode.None } },
            { RasterizerType.Wireframe, new() { FillMode = FillMode.WireFrame, ScissorTestEnable = false, CullMode = CullMode.None } }
        };

        private static readonly Dictionary<BlendType, BlendState> BlendMap = new()
        {
            { BlendType.Default, BlendState.AlphaBlend },
            { BlendType.Additive, BlendState.Additive },
            { BlendType.AlphaBlend, BlendState.AlphaBlend },
            { BlendType.NonPremultiplied, BlendState.NonPremultiplied },
            { BlendType.Opaque, BlendState.Opaque }
        };

        private static readonly Dictionary<SamplerType, SamplerState> SamplerMap = new()
        {
            { SamplerType.Default, SamplerState.LinearClamp },
            { SamplerType.AnisotropicClamp, SamplerState.AnisotropicClamp },
            { SamplerType.AnisotropicWrap, SamplerState.AnisotropicWrap },
            { SamplerType.LinearClamp, SamplerState.LinearClamp },
            { SamplerType.LinearWrap, SamplerState.LinearWrap },
            { SamplerType.PointClamp, SamplerState.PointClamp },
            { SamplerType.PointWrap, SamplerState.PointWrap },
        };

        private static readonly Dictionary<DepthStencilType, DepthStencilState> DepthStencilMap = new()
        {
            { DepthStencilType.Default, DepthStencilState.None },
            { DepthStencilType.DepthRead, DepthStencilState.DepthRead },
            { DepthStencilType.None, DepthStencilState.None }
        };

        private static readonly Dictionary<(DepthStencilType Type, int Reference, int ReadMask, int WriteMask), DepthStencilState> CustomDepthStencilMap = new();

        public RasterizerState RasterizerState => RasterizerMap[RasterizerType];
        public BlendState BlendState => BlendMap[BlendType];
        public SamplerState SamplerState => SamplerMap[SamplerType];
        public DepthStencilState DepthStencilState
        {
            get
            {
                if (DepthStencilMap.TryGetValue(DepthStencilType, out DepthStencilState Existing))
                {
                    return Existing;
                }

                var key = (DepthStencilType, StencilReference, StencilReadMask, StencilWriteMask);
                if (!CustomDepthStencilMap.TryGetValue(key, out DepthStencilState Result))
                {
                    Result = CreateDepthStencilState(DepthStencilType, StencilReference, StencilReadMask, StencilWriteMask);
                    CustomDepthStencilMap.Add(key, Result);
                }
                return Result;
            }
        }

        private static DepthStencilState CreateDepthStencilState(DepthStencilType type, int reference, int readMask, int writeMask)
            => type switch
            {
                DepthStencilType.StencilWriteIncrement => new DepthStencilState
                {
                    StencilEnable = true,
                    ReferenceStencil = reference,
                    StencilMask = readMask,
                    StencilWriteMask = writeMask,
                    StencilFunction = CompareFunction.Equal,
                    StencilPass = StencilOperation.Increment,
                    StencilFail = StencilOperation.Keep,
                    StencilDepthBufferFail = StencilOperation.Keep,
                    DepthBufferEnable = false,
                },
                DepthStencilType.StencilReadEqual => new DepthStencilState
                {
                    StencilEnable = true,
                    ReferenceStencil = reference,
                    StencilMask = readMask,
                    StencilWriteMask = writeMask,
                    StencilFunction = CompareFunction.Equal,
                    StencilPass = StencilOperation.Keep,
                    StencilFail = StencilOperation.Keep,
                    StencilDepthBufferFail = StencilOperation.Keep,
                    DepthBufferEnable = false,
                },
                DepthStencilType.StencilRestoreDecrement => new DepthStencilState
                {
                    StencilEnable = true,
                    ReferenceStencil = reference,
                    StencilMask = readMask,
                    StencilWriteMask = writeMask,
                    StencilFunction = CompareFunction.Equal,
                    StencilPass = StencilOperation.Decrement,
                    StencilFail = StencilOperation.Keep,
                    StencilDepthBufferFail = StencilOperation.Keep,
                    DepthBufferEnable = false,
                },
                _ => throw new NotImplementedException($"Unrecognized {nameof(DepthStencilType)}: {type}")
            };

        public void BeginDraw(SpriteBatch SB) => SB.Begin(Sort, BlendState, SamplerState, DepthStencilState, RasterizerState, Effect, Transform);
    }
}
