using System;
using MGUI.Shared.Input;
using MGUI.Shared.Rendering;

namespace MGUI.Backend.MonoGame
{
    public sealed class MonoGameBackendSession<THost>
        where THost : IRenderHost
    {
        public THost Host { get; }
        public MainRenderer Renderer { get; }

        internal MonoGameBackendSession(THost host, MainRenderer renderer)
        {
            Host = host ?? throw new ArgumentNullException(nameof(host));
            Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        }
    }

    public static class MonoGameBackendBootstrap
    {
        public static MonoGameBackendSession<THost> Create<THost>(THost host, IRawInputSource rawInputSource = null)
            where THost : IRenderHost
        {
            ArgumentNullException.ThrowIfNull(host);

            MainRenderer renderer = new(host, rawInputSource ?? new MonoGameRawInputSource());
            return new(host, renderer);
        }
    }
}