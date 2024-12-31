using System;
using System.IO;
using System.Threading.Tasks;
using UILayout;

namespace ChartConverter
{
    public class MainInterface : VerticalStack
    {
        string saveFolder;
        string saveFile;

        ConvertOptions convertOptions;

        NinePatchWrapper topSection;

        TextBlock songOutputText;

        VerticalStack fileStack;
        VerticalStack folderStack;

        VerticalStack convertStack;
        TextBlock currentlyConverting;
        int songsConverted;
        TextBlock songsConvertedText;
        TextButton convertButton;       

        bool abortConversion;
        bool convertRunning;

        static MainInterface()
        {
            Layout.Current.DefaultOutlineNinePatch = Layout.Current.AddImage("ShadowedOutline");

            Layout.Current.DefaultPressedNinePatch = Layout.Current.AddImage("ButtonPressed");
            Layout.Current.DefaultUnpressedNinePatch = Layout.Current.AddImage("ButtonUnpressed");

            Layout.Current.DefaultDragImage = Layout.Current.GetImage("ButtonPressed");

            Layout.Current.AddImage("Outline");

            Layout.Current.DefaultForegroundColor = UIColor.Black;
        }

        public MainInterface()
        {
            saveFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ChartConverter");
            saveFile = Path.Combine(saveFolder, "Options.xml");

            try
            {
                if (!Directory.Exists(saveFolder))
                {
                    Directory.CreateDirectory(saveFolder);
                }

                convertOptions = ConvertOptions.Load(saveFile);
            }
            catch { }

            if (convertOptions == null)
            {
                convertOptions = new ConvertOptions();

                convertOptions.ParseFiles.Add(@"C:\Program Files (x86)\Steam\steamapps\common\Rocksmith2014\songs.psarc");
                convertOptions.ParseFolders.Add(@"C:\Program Files (x86)\Steam\steamapps\common\Rocksmith2014\dlc");
            }

            HorizontalAlignment = EHorizontalAlignment.Stretch;
            VerticalAlignment = EVerticalAlignment.Stretch;
            Padding = new LayoutPadding(5);
            BackgroundColor = UIColor.Lerp(UIColor.White, UIColor.Black, 0.1f);

            topSection = new NinePatchWrapper(Layout.Current.GetImage("Outline"))
            {
                HorizontalAlignment = EHorizontalAlignment.Stretch,
                VerticalAlignment = EVerticalAlignment.Top
            };
            Children.Add(topSection);

            VerticalStack topStack = new VerticalStack()
            {
                HorizontalAlignment = EHorizontalAlignment.Stretch,
                ChildSpacing = 5
            };
            topSection.Child = topStack;

            topStack.Children.Add(new TextBlock("Song Output Folder:")
            {
                Padding = new LayoutPadding(0, 10)
            });

            HorizontalStack pathStack = new HorizontalStack()
            {
                ChildSpacing = 10
            };
            topStack.Children.Add(pathStack);

            pathStack.Children.Add(songOutputText = new TextBlock(convertOptions.SongOutputPath)
            {
                VerticalAlignment = EVerticalAlignment.Center
            });
            
            pathStack.Children.Add(new TextButton("Select")
            {
                ClickAction = SelectSongPath
            });

            topStack.Children.Add(new TextBlock("Psarc Files:")
            {
                Padding = new LayoutPadding(0, 10)
            });

            fileStack = new VerticalStack()
            {
                HorizontalAlignment = EHorizontalAlignment.Stretch,
                ChildSpacing = 5
            };
            topStack.Children.Add(fileStack);

            topStack.Children.Add(new TextButton("Add File")
            {
                ClickAction = AddFile
            });

            topStack.Children.Add(new TextBlock("Psarc Folders:")
            {
                Padding = new LayoutPadding(0, 10)
            });

            folderStack = new VerticalStack
            {
                HorizontalAlignment = EHorizontalAlignment.Stretch,
                ChildSpacing = 5
            };
            topStack.Children.Add(folderStack);

            topStack.Children.Add(new TextButton("Add Folder")
            {
                ClickAction = AddFolder
            });

            UpdateSources();

            NinePatchWrapper bottomSection = new NinePatchWrapper(Layout.Current.GetImage("Outline"))
            {
                HorizontalAlignment = EHorizontalAlignment.Stretch,
                VerticalAlignment = EVerticalAlignment.Stretch
            };
            Children.Add(bottomSection);

            Dock convertDock = new Dock();
            bottomSection.Child = convertDock;

            convertStack = new VerticalStack()
            {
                HorizontalAlignment = EHorizontalAlignment.Stretch,
                ChildSpacing = 10,
            };
            convertDock.Children.Add(convertStack);

            convertStack.Children.Add(new TextBlock("Conversion status:")
            {
                Padding = new LayoutPadding(0, 10)
            });

            songsConvertedText = new TextBlock("");
            convertStack.Children.Add(songsConvertedText);

            currentlyConverting = new TextBlock("") { Padding = new LayoutPadding(0, 3) };
            convertStack.Children.Add(currentlyConverting);

            convertButton = new TextButton("Convert Files")
            {
                VerticalAlignment = EVerticalAlignment.Bottom,
                ClickAction = ConvertFiles
            };

            convertDock.Children.Add(convertButton);
        }

        public void SaveOptions()
        {
            convertOptions.Save(saveFile);
        }

        void SelectSongPath()
        {
            string newPath = Layout.Current.GetFolder(convertOptions.SongOutputPath);

            if (!string.IsNullOrEmpty(newPath))
            {
                convertOptions.SongOutputPath = newPath;

                songOutputText.Text = convertOptions.SongOutputPath;

                UpdateContentLayout();

                SaveOptions();
            }
        }

        void AddFolder()
        {
            string newPath = Layout.Current.GetFolder(null);

            if (!string.IsNullOrEmpty(newPath) && !convertOptions.ParseFolders.Contains(newPath))
            {
                convertOptions.ParseFolders.Add(newPath);

                UpdateSources();
            }
        }

        void DeleteFolder(string path)
        {
            convertOptions.ParseFolders.Remove(path);

            UpdateSources();
        }

        void AddFile()
        {
            string newFile = Layout.Current.GetFile("Add File", "Psarc Files", "psarc");

            if (!string.IsNullOrEmpty(newFile) && !convertOptions.ParseFiles.Contains(newFile))
            {
                convertOptions.ParseFiles.Add(newFile);

                UpdateSources();
            }
        }

        void DeleteFile(string file)
        {
            convertOptions.ParseFiles.Remove(file);

            UpdateSources();
        }

        void UpdateSources()
        {
            folderStack.Children.Clear();
            
            foreach (string folder in convertOptions.ParseFolders)
            {
                HorizontalStack stack = new HorizontalStack()
                {
                    ChildSpacing = 10
                };
                folderStack.Children.Add(stack);

                stack.Children.Add(new TextBlock(folder)
                {
                    VerticalAlignment = EVerticalAlignment.Center
                });

                stack.Children.Add(new TextButton("X")
                {
                    TextColor = UIColor.Lerp(UIColor.Red, UIColor.Black, 0.5f),
                    ClickAction = delegate { DeleteFolder(folder); },                    
                });
            }

            fileStack.Children.Clear();

            foreach (string file in convertOptions.ParseFiles)
            {
                HorizontalStack stack = new HorizontalStack()
                {
                    ChildSpacing = 10
                };
                fileStack.Children.Add(stack);

                stack.Children.Add(new TextBlock(file)
                {
                    VerticalAlignment = EVerticalAlignment.Center
                });

                stack.Children.Add(new TextButton("X")
                {
                    TextColor = UIColor.Lerp(UIColor.Red, UIColor.Black, 0.5f),
                    ClickAction = delegate { DeleteFile(file); },
                });
            }

            UpdateContentLayout();

            SaveOptions();
        }

        bool UpdateConvert(string text)
        {
            currentlyConverting.Text = text;

            songsConverted++;
            songsConvertedText.Text = songsConverted + " Songs";

            convertStack.UpdateContentLayout();

            if (abortConversion)
            {
                abortConversion = false;

                return false;
            }

            return true;
        }

        void ConvertFiles()
        {
            if (string.IsNullOrEmpty(convertOptions.SongOutputPath))
            {
                Layout.Current.ShowContinuePopup("Please select a song output path.");

                return;
            }

            if (convertRunning)
            {
                abortConversion = true;
            }
            else
            {
                convertRunning = true;

                convertButton.Text = "Abort Conversion";
                UpdateContentLayout();

                Task.Run(() =>
                {
                    DoConvert();

                    convertRunning = false;

                    convertButton.Text = "Convert Files";

                    UpdateConvert("Finished");

                    convertButton.UpdateContentLayout();
                });
            }
        }

        void DoConvert()
        {
            songsConverted = 0;

            PsarcUtil.PsarcConverter converter = new PsarcUtil.PsarcConverter(convertOptions.SongOutputPath, convertAudio: false);
            converter.UpdateAction = UpdateConvert;

            foreach (string file in convertOptions.ParseFiles)
            {
                try
                {
                    converter.ConvertPsarc(file);
                }
                catch { }
            }

            foreach (string folder in convertOptions.ParseFolders)
            {
                try
                {
                    converter.ConvertFolder(folder);
                }
                catch { }
            }

            return;
        }
    }
}
