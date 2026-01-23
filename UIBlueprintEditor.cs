using Frosty.Core;
using Frosty.Core.Controls;
using Frosty.Core.Controls.Editors;
using Frosty.Core.Screens;
using Frosty.Core.Windows;
using FrostySdk.Ebx;
using FrostySdk.Interfaces;
using FrostySdk.IO;
using FrostySdk.Managers;
using FrostySdk.Resources;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Media.TextFormatting;
using System.Windows.Threading;
using TexturePlugin;
using static System.Net.Mime.MediaTypeNames;

namespace UIBlueprintEditor
{
    [TemplatePart(Name = PART_SwitchView, Type = typeof(Button))]
    [TemplatePart(Name = PART_DefaultEditorLayer, Type = typeof(Grid))]
    [TemplatePart(Name = PART_UIEditorLayer, Type = typeof(Grid))]
    [TemplatePart(Name = PART_AddObject, Type = typeof(Button))]
    [TemplatePart(Name = PART_UISize, Type = typeof(Grid))]
    [TemplatePart(Name = PART_UICanvas, Type = typeof(Canvas))]
    [TemplatePart(Name = PART_Refresh, Type = typeof(Button))]
    [TemplatePart(Name = PART_Precise, Type = typeof(Button))]
    [TemplatePart(Name = PART_PreciseImage, Type = typeof(Button))]
    [TemplatePart(Name = PART_UIComponentInfo, Type = typeof(TextBlock))]
    [TemplatePart(Name = PART_Unhide, Type = typeof(Button))]
    [TemplatePart(Name = PART_UISizeText, Type = typeof(TextBlock))]
    public class UIEditor : FrostyAssetEditor
    {
        private const string PART_SwitchView = "PART_SwitchView";
        private const string PART_DefaultEditorLayer = "PART_DefaultEditorLayer";
        private const string PART_UIEditorLayer = "PART_UIEditorLayer";
        private const string PART_AddObject = "PART_AddObject";
        private const string PART_UISize = "PART_UISize";
        private const string PART_TemplateUI = "PART_TemplateUI";
        private const string PART_UICanvas = "PART_UICanvas";
        private const string PART_Refresh = "PART_Refresh";
        private const string PART_Precise = "PART_Precise";
        private const string PART_PreciseImage = "PART_PreciseImage";
        private const string PART_UIComponentInfo = "PART_UIComponentInfo";
        private const string PART_Unhide = "PART_Unhide";
        private const string PART_UISizeText = "PART_UISizeText";

        private Button _switchViewButton;
        private FrameworkElement _uiEditorLayer;
        private FrameworkElement _defaultEditorLayer;
        private Button _addObjectButton;
        private FrameworkElement _uiSize;
        private Canvas _uiCanvas;
        private Button _refreshButton;
        private Button _preciseButton;
        private System.Windows.Controls.Image _preciseImage;
        private TextBlock _uiComponentInfo;
        private Button _unhideButton;
        private TextBlock _uiSizeText;

        private bool isEditorActive = false;

        public UIEditor(ILogger inLogger) : base(inLogger)
        {
            // App.Logger.Log(App.SelectedAsset.Type.ToString());
        }
        static UIEditor()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(UIEditor), new FrameworkPropertyMetadata(typeof(UIEditor)));
        }

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _uiEditorLayer = GetTemplateChild(PART_UIEditorLayer) as FrameworkElement;
            _defaultEditorLayer = GetTemplateChild(PART_DefaultEditorLayer) as FrameworkElement;

            _switchViewButton = GetTemplateChild(PART_SwitchView) as Button;
            _switchViewButton.Click += SwitchViewButton_Click;

            _addObjectButton = GetTemplateChild(PART_AddObject) as Button;
            _addObjectButton.Click += AddObjectButton_Click;

            _uiSize = GetTemplateChild(PART_UISize) as FrameworkElement;

            _uiCanvas = GetTemplateChild(PART_UICanvas) as Canvas;

            _refreshButton = GetTemplateChild(PART_Refresh) as Button;
            _refreshButton.Click += RefreshButton_Click;

            _preciseButton = GetTemplateChild(PART_Precise) as Button;
            _preciseButton.Click += PreciseButton_Click;

            _preciseImage = GetTemplateChild(PART_PreciseImage) as System.Windows.Controls.Image;

            _uiComponentInfo = GetTemplateChild(PART_UIComponentInfo) as TextBlock;

            _unhideButton = GetTemplateChild(PART_Unhide) as Button;
            _unhideButton.Click += UnhideButton_Click;

            _uiSizeText = GetTemplateChild(PART_UISizeText) as TextBlock;
        }

        // switches between the default editor and the ui editor
        private void SwitchViewButton_Click(object sender, RoutedEventArgs e)
        {
            // toggles the bool
            isEditorActive = !isEditorActive;
            if (isEditorActive)
            {
                // hides the default view and shows the editor view 
                _uiEditorLayer.Visibility = Visibility.Visible;
                _defaultEditorLayer.Visibility = Visibility.Hidden;

                // gets the opened asset as an EbxAssetEntry
                EbxAssetEntry openedAsset = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;

                if (openedAsset == null)
                    return;

                /*
                // broken loading screen
                FrostyTaskWindow.Show("Loading UI...", "", (task) =>
                {
                    LoadUI(openedAsset, false, null);
                });
                */

                // loads all the ui elements with the openedAsset, isWidget as false
                // and no widget canvas since we aren't loading a widget
                LoadUI(openedAsset, false, null);
            }
            else
            {
                // hides the editor view and shows the default view
                _uiEditorLayer.Visibility = Visibility.Hidden;
                _defaultEditorLayer.Visibility = Visibility.Visible;
            }
        }

        private static TextureExporter s_exporter = new TextureExporter();

        // some settings that can be customized
        readonly bool createImages = Config.Get<bool>("RenderTextures", true);
        readonly bool createWidgets = Config.Get<bool>("RenderWidgets", true);
        readonly bool createText = Config.Get<bool>("RenderText", true);

        // this is used for the precise movement / snapping
        int roundTo = 1;

        bool dragging = false;
        readonly bool debugging = false; // make this 'true' to have a lot of useful info logged


        // these dictionaries are used later to reference certain values using the TextureId as the key

        Dictionary<dynamic, dynamic> mappingIdToMapping = new Dictionary<dynamic, dynamic>();
        Dictionary<dynamic, dynamic> mappingMinValue = new Dictionary<dynamic, dynamic>();
        Dictionary<dynamic, dynamic> mappingMaxValue = new Dictionary<dynamic, dynamic>();
        Dictionary<dynamic, BitmapImage> mappingTexture = new Dictionary<dynamic, BitmapImage>();

        // this is a separate method so we can check the TextureId for each bitmap entity
        // which should make loading times faster since a texture doesn't need to be created for every output entry
        private void GetTextures(dynamic rootObject, string textureId)
        {
            // loops through every texture mapping asset in the ui blueprint
            foreach (var textureItem in rootObject.Object.Internal.TextureMappings)
            {
                if (debugging)
                {
                    App.Logger.Log("texture");
                }

                // get the texture mapping asset from the PointerRef
                var textureMapGuid = ((PointerRef)textureItem).External.FileGuid;
                var textureMapEbx = App.AssetManager.GetEbxEntry(textureMapGuid);

                EbxAsset textureMapAsset = App.AssetManager.GetEbx(textureMapEbx);
                dynamic rootObjectTextureMap = textureMapAsset.RootObject;

                // loops through each output in the texture mapping asset
                foreach (dynamic outputEntry in rootObjectTextureMap.Output)
                {
                    // if the texture isn't used in the ui we're loading we will skip creating the texture
                    if (outputEntry.Texture != textureId)
                    {
                        // sometimes there can be more than one texture id of the same name
                        if (!mappingIdToMapping.ContainsKey(outputEntry.Id))
                        {
                            var min = outputEntry.Min;
                            var max = outputEntry.Max;
                            var textureRef = outputEntry.Texture;

                            var textureGuid = ((PointerRef)textureRef).External.FileGuid;
                            var textureEbx = App.AssetManager.GetEbxEntry(textureGuid);

                            var textureAsset = App.AssetManager.GetEbx(textureEbx);
                            dynamic rootObjectTexture = textureAsset.RootObject;
                            ulong textureRes = ((dynamic)rootObjectTexture).Resource;

                            // texture section by NM (thanks lol)

                            Texture texture = App.AssetManager.GetResAs<Texture>(App.AssetManager.GetResEntry(textureRes));

                            mappingIdToMapping.Add(outputEntry.Id, outputEntry);
                            mappingMinValue.Add(outputEntry.Id, min);
                            mappingMaxValue.Add(outputEntry.Id, max);

                            string tempFolder = Path.GetTempPath();

                            // Temporary filename.
                            string path = Path.Combine(tempFolder,
                                string.Format("{0:X16}.png", texture.ResourceId));

                            if (!File.Exists(path))
                            {
                                // `TextureExporter` can't export to a `Stream`, so we'll need to export to the disk first.
                                s_exporter.Export(texture, path, "*.png");
                            }

                            // Read the newly exported image into a `Bitmap`.
                            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                            var bitmap = new BitmapImage();

                            bitmap.BeginInit();
                            bitmap.StreamSource = stream;
                            bitmap.EndInit();

                            mappingTexture.Add(outputEntry.Id, bitmap);
                        }
                    }
                }
            }
        }

        // loads every asset/component in the ui blueprint that you're currently on
        private void LoadUI(EbxAssetEntry ebxEntry, bool isWidget, Canvas widgetCanvas)
        {
            EbxAsset asset = App.AssetManager.GetEbx(ebxEntry);
            dynamic rootObject = asset.RootObject;

            if (debugging)
            {
                App.Logger.Log("");
                App.Logger.Log("---- " + rootObject.Name + " ----");
            }

            float mainSizeX = rootObject.Object.Internal.Size.X;
            float mainSizeY = rootObject.Object.Internal.Size.Y; 

            if (isWidget == false)
            {
                // if its not a widget we set the screen size
                _uiCanvas.Children.Clear();
                _uiSize.Width = mainSizeX;
                _uiSize.Height = mainSizeY;

                _uiSizeText.Text = string.Format("Size: {0}, {1}", mainSizeX, mainSizeY);
            }

            // loops through the "Layers"
            foreach (var layer in rootObject.Object.Internal.Layers)
            {
                // loops through each component in each layer
                foreach (var uiComponent in layer.Internal.Elements)
                {
                    // the ui will only render if the Visible property of the layer is true
                    if (layer.Internal.Visible == true)
                    {
                        double offsetX = (double)uiComponent.Internal.Offset.X;
                        double offsetY = (double)uiComponent.Internal.Offset.Y;

                        double anchorX = (double)(uiComponent.Internal.Anchor.X);
                        double anchorY = (double)(uiComponent.Internal.Anchor.Y);

                        double width = (double)(uiComponent.Internal.Size.X);
                        double height = (double)(uiComponent.Internal.Size.Y);
                        double x = (double)(uiComponent.Internal.Offset.X);
                        double y = (double)(uiComponent.Internal.Offset.Y);

                        
                        if (debugging)
                        {
                            App.Logger.Log("{0} Offset: {1} {2}, Size: {3} {4}, Anchor: {5} {6}",
                            uiComponent.Internal.InstanceName,
                            offsetX.ToString(), offsetY.ToString(),
                            width.ToString(), height.ToString(),
                            anchorX.ToString(), anchorY.ToString());
                        }

                        // these are the positions used for every ui element we'll create
                        double finalX = anchorX * (mainSizeX - width) + x;
                        double finalY = anchorY * (mainSizeY - height) + y;

                        var componentName = uiComponent.Internal.ToString();

                        if ((componentName == "FrostySdk.Ebx.UIElementBitmapEntityData" || componentName == "FrostySdk.Ebx.PVZUIElementBitmapEntityData" || componentName == "FrostySdk.Ebx.PVZUIElementDynamicBitmapEntityData") && createImages == true)
                        {
                            try
                            {
                                if (uiComponent.Internal.Visible == true)
                                {
                                    string textureMapId = uiComponent.Internal.TextureId;

                                    // gets all the textures needed for this bitmap
                                    GetTextures(rootObject, textureMapId);

                                    // canvas is used to group each ui component, will be useful for the draggable ui
                                    var canvas = new Canvas
                                    {
                                        Width = width,
                                        Height = height,
                                        Tag = Convert.ToString(uiComponent.Internal.__InstanceGuid)
                                    };

                                    var image = new System.Windows.Controls.Image
                                    {
                                        Width = width,
                                        Height = height,
                                        Stretch = Stretch.Fill,
                                    };

                                    // this TextBlock is only for debugging to see the bitmap names
                                    var tb = new TextBlock
                                    {
                                        Width = width,
                                        Height = height,
                                        Foreground = Brushes.Lime,
                                        Text = uiComponent.Internal.InstanceName,
                                    };

                                    // gets the needed texture from the dictionary created earlier with the texture map id as the key
                                    var texture = mappingTexture[textureMapId];

                                    // sets the source of the texture to the exported texture
                                    image.Source = texture;

                                    // all the values needed for cropping a bitmap
                                    // they are multiplied by the width/height because min/max values start from 0 - 1
                                    double minX = mappingMinValue[textureMapId].x * width;
                                    double minY = mappingMinValue[textureMapId].y * height;
                                    double maxX = mappingMaxValue[textureMapId].x * width;
                                    double maxY = mappingMaxValue[textureMapId].y * height;

                                    Point min = new Point(minX, minY);
                                    Point max = new Point(maxX, maxY);
                                    
                                    // Clip is used to crop the texture
                                    image.Clip = new RectangleGeometry(new Rect(min, max));
                                    RenderOptions.SetBitmapScalingMode(image, bitmapScalingMode: BitmapScalingMode.Fant);

                                    // scale up to previous size
                                    double croppedWidth = maxX - minX;
                                    double croppedHeight = maxY - minY;

                                    double scaleX = width / croppedWidth;
                                    double scaleY = height / croppedHeight;

                                    var transformGroup = new TransformGroup();
                                    transformGroup.Children.Add(new TranslateTransform(-minX, -minY));
                                    transformGroup.Children.Add(new ScaleTransform(scaleX, scaleY));

                                    image.RenderTransform = transformGroup;

                                    image.Opacity = uiComponent.Internal.Alpha;

                                    // sets the position
                                    Canvas.SetLeft(canvas, finalX);
                                    Canvas.SetTop(canvas, finalY);

                                    if (isWidget)
                                    {
                                        widgetCanvas.Children.Add(canvas);
                                        canvas.Children.Add(image);
                                        //canvas.Children.Add(tb);

                                        // comment out if you don't need text on the image
                                    }
                                    else
                                    {
                                        _uiCanvas.Children.Add(canvas);
                                        canvas.Children.Add(image);
                                        //canvas.Children.Add(tb);

                                        // comment out if you don't need text on the image

                                        ControlUI(canvas); // this will make it so you can drag the canvas around
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                App.Logger.Log("Something went wrong. InstanceName: " + uiComponent.Internal.InstanceName + ". Exception: " + ex);
                                // "An item with the same key" error sometimes happens
                            }
                        }
                        else if ((componentName == "FrostySdk.Ebx.UIElementTextFieldEntityData" || componentName == "FrostySdk.Ebx.PVZUIElementTextFieldEntityData") && createText == true)
                        {
                            if (uiComponent.Internal.Visible == true)
                            {
                                var canvas = new Canvas
                                {
                                    Width = width,
                                    Height = height,
                                    Tag = Convert.ToString(uiComponent.Internal.__InstanceGuid)
                                };

                                var tb = new TextBlock
                                {
                                    Width = width,
                                    Height = height,
                                };

                                string sid = uiComponent.Internal.Text.Sid;
                                string fieldText = uiComponent.Internal.FieldText;

                                // gets the colour from the xyz value directly from the component
                                // most text fields use a FontEffect for outlines and colours but this is just easier to read from
                                var colorR = (byte)Math.Round(uiComponent.Internal.Color.x * 255);
                                var colorG = (byte)Math.Round(uiComponent.Internal.Color.y * 255);
                                var colorB = (byte)Math.Round(uiComponent.Internal.Color.z * 255);

                                // font style
                                var fontGuid = ((PointerRef)uiComponent.Internal.FontStyle).External.FileGuid;
                                var fontEbx = App.AssetManager.GetEbxEntry(fontGuid);

                                EbxAsset fontAsset = App.AssetManager.GetEbx(fontEbx);
                                dynamic rootObjectFont = fontAsset.RootObject;

                                // if there's no text (instead it's set with a property connection or something) it will use InstanceName
                                if (sid != "")
                                {
                                    // if its an id it will use the string of the id
                                    if (sid.StartsWith("ID_"))
                                    {
                                        tb.Text = LocalizedStringDatabase.Current.GetString(sid);
                                    }
                                    else
                                    {
                                        tb.Text = sid;
                                    }
                                }
                                // some text fields use "FieldText"
                                else if (fieldText != "")
                                {
                                    if (sid.StartsWith("ID_"))
                                    {
                                        tb.Text = LocalizedStringDatabase.Current.GetString(fieldText);
                                    }
                                    else
                                    {
                                        tb.Text = fieldText;
                                    }
                                }
                                else
                                {
                                    tb.Text = uiComponent.Internal.InstanceName;
                                }

                                // basic settings
                                tb.ClipToBounds = uiComponent.Internal.ClipToRect;
                                if (uiComponent.Internal.Password == true)
                                {
                                    tb.Text = new string('*', tb.Text.Length);
                                }
                                if (uiComponent.Internal.Text.Wordwrap == true)
                                {
                                    tb.TextWrapping = TextWrapping.Wrap;
                                }

                                // setting the actual font
                                var fontEbxPath = rootObjectFont.Hd.Internal.FontLookup[0].FontAssetPath;

                                var fontEbxTTF = App.AssetManager.GetEbx(fontEbxPath);
                                ulong ttfRes = ((dynamic)fontEbxTTF.RootObject).FontResource;

                                ResAssetEntry ttfResEntry = App.AssetManager.GetResEntry(ttfRes);
                                
                                using (Stream ttfStream = App.AssetManager.GetRes(ttfResEntry))
                                {
                                    string fontName = "./#" + fontEbxTTF.RootObject.FontFamilyName;

                                    // 'HouseofTerror' font has a space for some reason
                                    if (fontName == "./#MonsterFonts-HouseofTerror")
                                    {
                                        fontName = "./#MonsterFonts HouseofTerror";
                                    }

                                    string tempFile = Path.Combine(Path.GetTempPath(), 
                                        string.Format("{0:X16}.ttf", fontEbxTTF.RootObject.FontResource));

                                    if (!File.Exists(tempFile))
                                    {
                                        using (var fs = new FileStream(tempFile, FileMode.Create, FileAccess.Write, FileShare.Read))
                                        {
                                            ttfStream.CopyTo(fs);
                                        }
                                    }
                                    
                                    tb.FontFamily = new FontFamily(new Uri(tempFile, UriKind.Absolute), fontName);
                                }

                                tb.FontSize = (double)rootObjectFont.Hd.Internal.PointSize;
                                tb.Foreground = new SolidColorBrush(Color.FromRgb(colorR, colorG, colorB));

                                tb.Margin = new Thickness();

                                // sets the alignment of the text
                                switch (uiComponent.Internal.Text.VerticalAlignment.ToString())
                                {
                                    case "UIElementAlignment_Top":
                                        tb.VerticalAlignment = VerticalAlignment.Top;
                                        break;
                                    case "UIElementAlignment_Center":
                                        tb.VerticalAlignment = VerticalAlignment.Center;
                                        break;
                                    case "UIElementAlignment_Bottom":
                                        tb.VerticalAlignment = VerticalAlignment.Bottom;
                                        break;
                                    default:
                                        tb.VerticalAlignment = VerticalAlignment.Center;
                                        break;
                                }

                                // they spelt horizontal wrong lol
                                switch (uiComponent.Internal.Text.HorizonalAlignment.ToString())
                                {
                                    case "UIElementAlignment_Left":
                                        tb.TextAlignment = TextAlignment.Left;
                                        break;
                                    case "UIElementAlignment_Center":
                                        tb.TextAlignment = TextAlignment.Center;
                                        break;
                                    case "UIElementAlignment_Right":
                                        tb.TextAlignment = TextAlignment.Right;
                                        break;
                                    default:
                                        tb.TextAlignment = TextAlignment.Center;
                                        break;
                                }

                                if (debugging)
                                {
                                    App.Logger.Log(uiComponent.Internal.Text.HorizonalAlignment.ToString());
                                    App.Logger.Log(uiComponent.Internal.Text.VerticalAlignment.ToString());

                                    App.Logger.Log(tb.HorizontalAlignment.ToString());
                                    App.Logger.Log(tb.VerticalAlignment.ToString());
                                }

                                // sets the position
                                Canvas.SetLeft(canvas, finalX);
                                Canvas.SetTop(canvas, finalY);

                                if (isWidget)
                                {
                                    widgetCanvas.Children.Add(canvas);
                                    canvas.Children.Add(tb);
                                }
                                else
                                {
                                    _uiCanvas.Children.Add(canvas);
                                    canvas.Children.Add(tb);

                                    ControlUI(canvas);
                                }
                            }
                        }
                        else if (componentName == "FrostySdk.Ebx.UIElementFillEntityData" || componentName == "FrostySdk.Ebx.PVZUIElementFillEntityData")
                        {
                            if (uiComponent.Internal.Visible == true)
                            {
                                var canvas = new Canvas
                                {
                                    Width = width,
                                    Height = height,
                                    Tag = Convert.ToString(uiComponent.Internal.__InstanceGuid)
                                };

                                var rect = new System.Windows.Shapes.Rectangle
                                {
                                    Width = width,
                                    Height = height,
                                };

                                // style
                                var fillGuid = ((PointerRef)uiComponent.Internal.Style).External.FileGuid;
                                var fillEbx = App.AssetManager.GetEbxEntry(fillGuid);

                                EbxAsset fillAsset = App.AssetManager.GetEbx(fillEbx);
                                dynamic rootObjectFill = fillAsset.RootObject;

                                var alpha = (float)rootObjectFill.BackgroundColor.Alpha;

                                var colorR = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.x * 255);
                                var colorG = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.y * 255);
                                var colorB = (byte)Math.Round(rootObjectFill.BackgroundColor.Rgb.z * 255);

                                rect.Fill = new SolidColorBrush(Color.FromRgb(colorR, colorG, colorB));
                                rect.Opacity = alpha;

                                // sets the position
                                Canvas.SetLeft(canvas, finalX);
                                Canvas.SetTop(canvas, finalY);

                                if (isWidget)
                                {
                                    widgetCanvas.Children.Add(canvas);
                                    canvas.Children.Add(rect);
                                }
                                else
                                {
                                    _uiCanvas.Children.Add(canvas);
                                    canvas.Children.Add(rect);

                                    ControlUI(canvas);
                                }
                            }
                        }
                        else if (componentName == "FrostySdk.Ebx.UIElementButtonEntityData")
                        {
                            // does nothing for buttons since they are basically just hitboxes
                        }
                        else if ((componentName == "FrostySdk.Ebx.UIElementWidgetReferenceEntityData") && createWidgets == true)
                        {
                            var canvasWidget = new Canvas
                            {
                                Tag = Convert.ToString(uiComponent.Internal.__InstanceGuid)
                            };

                            // gets the reference blueprint of the widget as an EBX
                            var widgetGuid = ((PointerRef)uiComponent.Internal.Blueprint).External.FileGuid;
                            var widgetEbx = App.AssetManager.GetEbxEntry(widgetGuid);

                            EbxAsset widgetAsset = App.AssetManager.GetEbx(widgetEbx);
                            dynamic rootObjectWidget = widgetAsset.RootObject;

                            var widgetSize = rootObjectWidget.Object.Internal.Size;

                            if (!uiComponent.Internal.UseElementSize)
                            {
                                canvasWidget.Width = widgetSize.X;
                                canvasWidget.Height = widgetSize.Y;
                            }
                            else
                            {
                                canvasWidget.Width = width;
                                canvasWidget.Height = height;
                            }

                            double widgetFinalX = anchorX * (mainSizeX - widgetSize.X) + x;
                            double widgetFinalY = anchorY * (mainSizeY - widgetSize.Y) + y;

                            Canvas.SetLeft(canvasWidget, widgetFinalX);
                            Canvas.SetTop(canvasWidget, widgetFinalY);

                            if (debugging)
                            {
                                App.Logger.Log("widget");
                            }

                            if (isWidget)
                            {
                                widgetCanvas.Children.Add(canvasWidget);
                            }
                            else
                            {
                                _uiCanvas.Children.Add(canvasWidget);

                                ControlUI(canvasWidget);
                            }

                            // repeats everything with the EBX of the widget to render everything that is inside the widget
                            LoadUI(widgetEbx, true, canvasWidget);
                        }
                        else
                        {
                            // creates a basic rectangle if its an unknown component
                            // if you're using this for another game this is what most ui elements will render as

                            App.Logger.Log("Unrecognized UI component");

                            var canvas = new Canvas
                            {
                                Width = width,
                                Height = height,
                                Tag = Convert.ToString(uiComponent.Internal.__InstanceGuid)
                            };

                            var rect = new System.Windows.Shapes.Rectangle
                            {
                                Width = width,
                                Height = height,
                                Fill = Brushes.Orange,
                                Opacity = 0.05,
                            };

                            var tb = new TextBlock
                            {
                                Text = uiComponent.Internal.InstanceName,
                                FontSize = 24,
                                Opacity = 0.2,
                            };

                            // sets the position
                            Canvas.SetLeft(canvas, finalX);
                            Canvas.SetTop(canvas, finalY);

                            if (isWidget)
                            {
                                widgetCanvas.Children.Add(canvas);
                                canvas.Children.Add(rect);
                                canvas.Children.Add(tb);
                            }
                            else
                            {
                                _uiCanvas.Children.Add(canvas);
                                canvas.Children.Add(rect);
                                canvas.Children.Add(tb);

                                ControlUI(canvas);
                            }
                        }
                    }
                }

                // update layout once everything is loaded
                _uiCanvas.UpdateLayout();
            }
        }

        Point startPosition;
        private void ControlUI(Canvas canvas)
        {
            canvas.MouseMove += CanvasMouseMove;
            canvas.MouseLeftButtonDown += CanvasMouseDown;
            canvas.MouseLeftButtonUp += CanvasMouseUp;

            canvas.MouseRightButtonDown += CanvasHideUI;
        }

        private void CanvasMouseDown(object sender, MouseButtonEventArgs e)
        {
            Canvas canvas = sender as Canvas;

            dragging = true;

            // gets the mouse position
            startPosition = Mouse.GetPosition(_uiCanvas);
        }

        private void CanvasMouseUp(object sender, MouseButtonEventArgs e)
        {
            dragging = false;

            Canvas canvas = sender as Canvas;

            // reset ZIndex after moving it
            Canvas.SetZIndex(canvas, 0);

            double roundedX = Math.Round((Canvas.GetLeft(canvas)) / roundTo) * roundTo;
            double roundedY = Math.Round((Canvas.GetTop(canvas)) / roundTo) * roundTo;

            float movedX = (float)roundedX;
            float movedY = (float)roundedY;

            // sets the position
            Canvas.SetLeft(canvas, roundedX);
            Canvas.SetTop(canvas, roundedY);

            // gets the Guid of the canvas from the tag created earlier so we can use it as an ebx asset
            var canvasGuid = canvas.Tag;

            _refreshButton.Visibility = Visibility.Visible;
            _preciseButton.Visibility = Visibility.Visible;
            _unhideButton.Visibility = Visibility.Visible;
            _uiSizeText.Visibility = Visibility.Visible;
            _uiComponentInfo.Visibility = Visibility.Visible;

            EbxAssetEntry ebxEntry = App.EditorWindow.GetOpenedAssetEntry() as EbxAssetEntry;

            EbxAsset asset = App.AssetManager.GetEbx(ebxEntry);
            dynamic rootObject = asset.RootObject;

            // goes through every ui component until the guid of it matches guid from the tag
            foreach (var layer in rootObject.Object.Internal.Layers)
            {
                foreach (var uiComponent in layer.Internal.Elements)
                {
                    var guid = uiComponent.Internal.__InstanceGuid;

                    if (debugging)
                    {
                        App.Logger.Log(Convert.ToString("Guid: " + guid));
                        App.Logger.Log(Convert.ToString("Canvas Guid: " + canvasGuid));
                    }

                    if (guid.ToString() == canvasGuid.ToString())
                    {
                        bool useAnchor = Config.Get<bool>("UseAnchor", false);

                        if (!useAnchor)
                        {
                            // if useAnchor is false, we remove the anchor and set the position with offset
                            uiComponent.Internal.Offset.X = movedX;
                            uiComponent.Internal.Offset.Y = movedY;

                            uiComponent.Internal.Anchor.X = 0;
                            uiComponent.Internal.Anchor.Y = 0;
                        }
                        else
                        {
                            // if useAnchor is true, we remove the offset and set the position with anchor
                            uiComponent.Internal.Anchor.X = movedX / rootObject.Object.Internal.Size.X;
                            uiComponent.Internal.Anchor.Y = movedY / rootObject.Object.Internal.Size.Y;

                            uiComponent.Internal.Offset.X = 0;
                            uiComponent.Internal.Offset.Y = 0;
                        }

                        // saves it to the ebx so that it will show up in game or in frosty
                        App.AssetManager.ModifyEbx(rootObject.Name, asset);

                        // refreshes the data explorer so that it shows as modified on the left
                        App.EditorWindow.DataExplorer.RefreshItems();

                        _uiComponentInfo.Text = 
                            string.Format(
                            "InstanceName: '{0}'\nOffset: {1}, {2}\nAnchor: {3}, {4}\n{5}", 
                            uiComponent.Internal.InstanceName, 
                            uiComponent.Internal.Offset.X, 
                            uiComponent.Internal.Offset.Y, 
                            uiComponent.Internal.Anchor.X, 
                            uiComponent.Internal.Anchor.Y, 
                            guid.ToString());

                        if (debugging)
                        {
                            App.Logger.Log("Saved Position");
                        }
                    }
                }
            }
        }

        private void CanvasMouseMove(object sender, MouseEventArgs e)
        {
            if (dragging)
            {
                Canvas canvas = sender as Canvas;

                // sets the ZIndex above everything else so it doesn't glitch when moving near other ui elements
                Canvas.SetZIndex(canvas, 9999);

                Point newPosition = Mouse.GetPosition(_uiCanvas);

                double left = Canvas.GetLeft(canvas);
                double top = Canvas.GetTop(canvas);

                Canvas.SetLeft(canvas, left + (newPosition.X - startPosition.X));
                Canvas.SetTop(canvas, top + (newPosition.Y - startPosition.Y));
                startPosition = newPosition;

                _refreshButton.Visibility = Visibility.Hidden;
                _preciseButton.Visibility = Visibility.Hidden;
                _unhideButton.Visibility = Visibility.Hidden;
                _uiSizeText.Visibility = Visibility.Hidden;
                _uiComponentInfo.Visibility = Visibility.Hidden;

                if (debugging)
                {
                    // this can make it very laggy if you have debugging on and not commented out

                    //App.Logger.Log(left.ToString());
                    //App.Logger.Log(top.ToString());

                    //App.Logger.Log(roundTo.ToString());
                }
            }
        }

        // right clicking will hide the ui, this is useful if some ui elements are in the way of something you wanna move
        private void CanvasHideUI(object sender, EventArgs e)
        {
            // it will only work if you aren't dragging a ui element otherwise it will be buggy
            if (!dragging)
            {
                Canvas canvas = sender as Canvas;
                canvas.Visibility = Visibility.Hidden;
            }
        }
        
        private void UnhideButton_Click(object sender, RoutedEventArgs e)
        {
            // loops through each canvas in _uiCanvas and sets them all visible
            foreach (Canvas canvas in _uiCanvas.Children)
            {
                canvas.Visibility = Visibility.Visible;
            }
        }

        // switches between the roundTo value and 1 which changes how precise ui dragging is
        // idk why i didn't just call it "Snapping" lol
        bool isPrecise = true;
        private void PreciseButton_Click(object sender, RoutedEventArgs e)
        {
            // toggles the bool
            isPrecise = !isPrecise;

            // gets the icon for the off and on icons
            ImageSource offIcon = new ImageSourceConverter().ConvertFromString("pack://application:,,,/UIBlueprintEditor;component/Images/Precise_OFF.png") as ImageSource;
            ImageSource onIcon = new ImageSourceConverter().ConvertFromString("pack://application:,,,/UIBlueprintEditor;component/Images/Precise_ON.png") as ImageSource;

            if (isPrecise)
            {
                roundTo = 1;

                App.Logger.Log("Precise Movement: ON");

                _preciseButton.ToolTip = "Precise Movement (ON)";
                _preciseImage.Source = onIcon;
            }
            else
            {
                roundTo = Config.Get<int>("PreciseMovementSetting", 25);

                App.Logger.Log("Precise Movement: OFF");

                _preciseButton.ToolTip = "Precise Movement (OFF)";
                _preciseImage.Source = offIcon;
            }
        }

        // refreshes the layout in case any ui values change
        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            _uiCanvas.UpdateLayout();
        }

        // unused thing that was gonna add ui elements
        private void AddObjectButton_Click(object sender, RoutedEventArgs e)
        {
            App.Logger.Log("added object");
        }
    }
}
