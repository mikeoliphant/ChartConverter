using System;
using UILayout;
using ChartConverter;

using var host = new ChartConverterHost(1024, 720, isFullscreen: false);

MonoGameLayout layout = new MonoGameLayout();

host.StartGame(layout);