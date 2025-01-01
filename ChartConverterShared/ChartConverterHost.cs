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
            UseEmbeddedResources = true;
            UsePremultipliedAlpha = false;
        }

        protected override void LoadContent()
        {
            using (Stream fontStream = OpenContentStream("Textures.Font.xml"))
            {
                XmlSerializer serializer = new XmlSerializer(typeof(SpriteFontDefinition));

                SpriteFontDefinition fontDef = serializer.Deserialize(fontStream) as SpriteFontDefinition;

                Layout.AddImage(fontDef.Name);

                Layout.DefaultFont = new UIFont { SpriteFont = UILayout.SpriteFont.CreateFromDefinition(fontDef) };
            }

            Layout.GraphicsContext.SingleWhitePixelImage = new UIImage("SingleWhitePixel");

            Layout.RootUIElement = new MainInterface();
        }
    }
}
