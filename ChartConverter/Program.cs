using System;
using UILayout;
using ChartConverter;

using var host = new ChartConverterHost(1024, 600, isFullscreen: false);

host.UseEmbeddedResources = true;

MonoGameLayout layout = new MonoGameLayout();

host.StartGame(layout);