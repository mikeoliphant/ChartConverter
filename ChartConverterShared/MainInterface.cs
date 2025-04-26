using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UILayout;
using SongFormat;
using PsarcUtil;

namespace ChartConverter
{
    public class MainInterface : VerticalStack
    {
        public static UIColor PanelBackgroundColor = new UIColor(50, 55, 65);
        public static UIColor PanelBackgroundColorDark = PanelBackgroundColor * 0.8f;
        public static UIColor PanelBackgroundColorDarkest = PanelBackgroundColor * 0.5f;
        public static UIColor PanelBackgroundColorLight = PanelBackgroundColor * 1.5f;
        public static UIColor PanelBackgroundColorLightest = PanelBackgroundColor * 3.0f;
        public static UIColor PanelForegroundColor = UIColor.Lerp(PanelBackgroundColor, UIColor.White, 0.75f);


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
        TextToggleButton convertPsarcButton;
        TextToggleButton convertRBButton;

        bool abortConversion;
        bool convertRunning;

        static MainInterface()
        {
            Layout.Current.DefaultOutlineNinePatch = Layout.Current.AddImage("PopupBackground");

            Layout.Current.DefaultPressedNinePatch = Layout.Current.AddImage("ButtonPressed");
            Layout.Current.DefaultUnpressedNinePatch = Layout.Current.AddImage("ButtonUnpressed");

            Layout.Current.DefaultDragImage = Layout.Current.GetImage("ButtonPressed");

            Layout.Current.AddImage("TabPanelBackground");
            Layout.Current.AddImage("TabBackground");
            Layout.Current.AddImage("TabForeground");

            Layout.Current.DefaultForegroundColor = UIColor.White;
        }

        public MainInterface()
        {
            HorizontalAlignment = EHorizontalAlignment.Stretch;
            VerticalAlignment = EVerticalAlignment.Stretch;
            Padding = new LayoutPadding(5);
            BackgroundColor = UIColor.Black;

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

                convertOptions.PsarcFiles.Add(@"C:\Program Files (x86)\Steam\steamapps\common\Rocksmith2014\songs.psarc");
                convertOptions.PsarcFolders.Add(@"C:\Program Files (x86)\Steam\steamapps\common\Rocksmith2014\dlc");
            }

            TabPanel tabPanel = new TabPanel(PanelBackgroundColorDark, UIColor.White, Layout.Current.GetImage("TabPanelBackground"), Layout.Current.GetImage("TabForeground"), Layout.Current.GetImage("TabBackground"), 5, 5);
            Children.Add(tabPanel);

            tabPanel.AddTab("General", GeneralTab());
            tabPanel.AddTab("Psarc", PsarcTab());
            tabPanel.AddTab("RockBand", RockBandTab());

            NinePatchWrapper bottomSection = new NinePatchWrapper(Layout.Current.GetImage("PopupBackground"))
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

            HorizontalStack buttonStack = new HorizontalStack()
            {
                VerticalAlignment = EVerticalAlignment.Bottom,
                ChildSpacing = 5
            };
            convertDock.Children.Add(buttonStack);

            convertButton = new TextButton("Convert Files")
            {
                ClickAction = ConvertFiles
            };

            buttonStack.Children.Add(convertButton);

            buttonStack.Children.Add(new TextBlock("Convert Psarc: ") { Padding = (20, 0, 0, 0), VerticalAlignment = EVerticalAlignment.Center });
            buttonStack.Children.Add(convertPsarcButton = new TextToggleButton("Yes", "No")
            {
                PressAction = delegate
                {
                    convertOptions.ConvertPsarc = convertPsarcButton.IsPressed;
                    SaveOptions();
                }
            });
            
            buttonStack.Children.Add(new TextBlock("Convert RockBand: ") { VerticalAlignment = EVerticalAlignment.Center } );
            buttonStack.Children.Add(convertRBButton = new TextToggleButton("Yes", "No")
            {
                PressAction = delegate
                {
                    convertOptions.ConvertRockBand = convertRBButton.IsPressed;
                    SaveOptions();
                }
            });

            convertPsarcButton.SetPressed(convertOptions.ConvertPsarc);
            convertRBButton.SetPressed(convertOptions.ConvertRockBand);
        }

        UIElement GeneralTab()
        {
            VerticalStack vStack = new VerticalStack()
            {
                HorizontalAlignment = EHorizontalAlignment.Stretch,
                ChildSpacing = 5
            };

            vStack.Children.Add(new TextBlock("Song Output Folder:")
            {
                Padding = new LayoutPadding(0, 10)
            });

            HorizontalStack pathStack = new HorizontalStack()
            {
                ChildSpacing = 10
            };
            vStack.Children.Add(pathStack);

            pathStack.Children.Add(songOutputText = new TextBlock(convertOptions.SongOutputPath)
            {
                VerticalAlignment = EVerticalAlignment.Center
            });

            pathStack.Children.Add(new TextButton("Select")
            {
                ClickAction = SelectSongPath
            });

            return vStack;
        }

        UIElement PsarcTab()
        {
            VerticalStack vStack = new VerticalStack()
            {
                HorizontalAlignment = EHorizontalAlignment.Stretch,
                ChildSpacing = 5
            };

            FolderFileList psarcFiles = new FolderFileList("Psarc Files:", convertOptions.PsarcFiles, Update, isFolder: false);
            psarcFiles.SetFilter("Psarc Files", "psarc");
            vStack.Children.Add(psarcFiles);


            FolderFileList psarcFolders = new FolderFileList("Psarc Folders:", convertOptions.PsarcFolders, Update, isFolder: true);
            vStack.Children.Add(psarcFolders);

            return vStack;
        }

        UIElement RockBandTab()
        {
            VerticalStack vStack = new VerticalStack()
            {
                HorizontalAlignment = EHorizontalAlignment.Stretch,
                ChildSpacing = 5
            };

            FolderFileList psarcFolders = new FolderFileList("RockBand Folders:", convertOptions.RockBandFolders, Update, isFolder: true);
            vStack.Children.Add(psarcFolders);

            return vStack;
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

        void Update()
        {
            UpdateContentLayout();
            SaveOptions();
        }

        EConvertOption UpdateRocksmithConvert(string artistName, string songName, string songDir)
        {
            currentlyConverting.Text = artistName + " - " + songName;

            songsConverted++;
            songsConvertedText.Text = songsConverted + " Songs";

            convertStack.UpdateContentLayout();

            if (abortConversion)
            {
                abortConversion = false;

                return EConvertOption.Abort;
            }

            return EConvertOption.Continue;
        }

        bool UpdateRockBandConvert(string text)
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

                    UpdateRockBandConvert("Finished");

                    convertButton.UpdateContentLayout();
                });
            }
        }

        void DoConvert()
        {
            songsConverted = 0;

            if (convertOptions.ConvertPsarc)
            {
                PsarcUtil.PsarcConverter converter = new PsarcUtil.PsarcConverter(convertOptions.SongOutputPath)
                {
                    OverwriteAudio = false,
                    OverwriteData = true,
                    UpdateAction = UpdateRocksmithConvert
                };

                foreach (string file in convertOptions.PsarcFiles)
                {
                    try
                    {
                        if (!converter.ConvertPsarc(file))
                        {
                            return;
                        }
                    }
                    catch { }
                }

                foreach (string folder in convertOptions.PsarcFolders)
                {
                    try
                    {
                        if (!converter.ConvertFolder(folder))
                            return;
                    }
                    catch { }
                }
            }

            if (convertOptions.ConvertRockBand)
            {
                var converter = new RockBandUtil.RockBandConverter(convertOptions.SongOutputPath, convertAudio: false);
                converter.UpdateAction = UpdateRockBandConvert;

                foreach (string folder in convertOptions.RockBandFolders)
                {
                    try
                    {
                        if (!converter.ConvertAll(folder))
                            return;
                    }
                    catch { }
                }
            }
        }
    }

    class FolderFileList : VerticalStack
    {
        List<string> itemList = null;
        VerticalStack itemStack = new VerticalStack();
        Action updateAction;
        bool isFolder = false;
        string filterName = null;
        string filterWildcard = null;

        public FolderFileList(string name, List<string> folderList, Action updateAction, bool isFolder)
        {
            this.itemList = folderList;
            this.updateAction = updateAction;
            this.isFolder = isFolder;

            ChildSpacing = 5;

            Children.Add(new TextBlock(name)
            {
                Padding = new LayoutPadding(0, 10)
            });

            Children.Add(itemStack = new VerticalStack()
            {
                ChildSpacing = 5
            });

            Children.Add(new TextButton("Add " + (isFolder ? "Folder" : "File"))
            {
                ClickAction = Add
            });

            UpdateSources();
        }

        public void SetFilter(string filterName, string filterWildcard)
        {
            this.filterName = filterName;
            this.filterWildcard = filterWildcard;
        }

        void Add()
        {
            string newPath = isFolder ? Layout.Current.GetFolder(null) : Layout.Current.GetFile(null, filterName, filterWildcard);

            if (!string.IsNullOrEmpty(newPath) && !itemList.Contains(newPath))
            {
                itemList.Add(newPath);

                UpdateSources();

                if (updateAction != null)
                    updateAction();
            }
        }

        void Delete(string path)
        {
            itemList.Remove(path);

            UpdateSources();

            if (updateAction != null)
                updateAction();
        }

        void UpdateSources()
        {
            itemStack.Children.Clear();

            foreach (string item in itemList)
            {
                HorizontalStack stack = new HorizontalStack()
                {
                    ChildSpacing = 10
                };
                itemStack.Children.Add(stack);

                stack.Children.Add(new TextBlock(item)
                {
                    VerticalAlignment = EVerticalAlignment.Center
                });

                stack.Children.Add(new TextButton("X")
                {
                    TextColor = UIColor.Lerp(UIColor.Red, UIColor.Black, 0.5f),
                    ClickAction = delegate { Delete(item); },
                });
            }
        }
    }
}
