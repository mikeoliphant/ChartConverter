using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using UILayout;

namespace ChartConverter
{
    public class ChartConverterHost : MonoGameHost
    {
        public ChartConverterHost(int screenWidth, int screenHeight, bool isFullscreen)
            : base(screenWidth, screenHeight, isFullscreen)
        {
            UsePremultipliedAlpha = false;
            Window.Title = "ChartConverter v0.1.10";
        }

        protected override void LoadContent()
        {
            Layout.RootUIElement = new MainInterface();
        }
    }
}
