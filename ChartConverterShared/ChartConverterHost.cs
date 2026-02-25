using UILayout;

namespace ChartConverter
{
    public class ChartConverterHost : MonoGameHost
    {
        public ChartConverterHost(int screenWidth, int screenHeight, bool isFullscreen)
            : base(screenWidth, screenHeight, isFullscreen)
        {
            UsePremultipliedAlpha = false;
            Window.Title = "ChartConverter v0.1.13";
        }

        protected override void LoadContent()
        {
            Layout.RootUIElement = new MainInterface();
        }
    }
}
