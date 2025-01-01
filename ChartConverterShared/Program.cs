using System;
using UILayout;
using ChartConverter;

namespace ChartPlayer
{
    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            using var host = new ChartConverterHost(1024, 720, isFullscreen: false);

            MonoGameLayout layout = new MonoGameLayout();

            host.StartGame(layout);
        }
    }
}