using Frosty.Controls;
using Frosty.Core;
using Frosty.Core.Controls.Editors;
using Frosty.Core.Misc;
using FrostySdk.Attributes;
using FrostySdk.IO;
using System.Collections.Generic;

namespace UIBlueprintEditor
{
    public class FrostyUIEditor : FrostyCustomComboDataEditor<string, string>
    {
    }

    [DisplayName("UI Editor Options")]
    public class UIEditorOptions : OptionsExtension
    {
        [Category("Movement")]
        [DisplayName("Precise Movement Setting")]
        [Description("Sets the amount of pixels the 'Precide Movement' snaps to (make sure Precise Movement is off to see the change)")]
        public int PreciseMovementSetting { get; set; } = 25;

        [Category("Movement")]
        [DisplayName("Use Anchor")]
        [Description("If true, dragging UI elements will move them with Anchor instead of Offset")]
        public bool UseAnchor { get; set; } = false;

        [Category("Types")]
        [DisplayName("Render Textures")]
        [Description("Textures are used for bitmap entities, they will be rendered if set to true, if not they will be an 'Unrecognized Component'")]
        public bool RenderTextures { get; set; } = true;

        [Category("Types")]
        [DisplayName("Render Text")]
        [Description("Text fields are used for text, they will be rendered if set to true, if not they will be an 'Unrecognized Component'")]
        public bool RenderText { get; set; } = true;

        [Category("Types")]
        [DisplayName("Render Widgets")]
        [Description("Widgets are used for grouping other UI blueprints, they will be rendered if set to true, if not they will be an 'Unrecognized Component' (can make loading times faster)")]
        public bool RenderWidgets { get; set; } = true;


        public override void Load()
        {
            PreciseMovementSetting = Config.Get<int>("PreciseMovementSetting", 25);
            UseAnchor = Config.Get<bool>("UseAnchor", false);

            RenderTextures = Config.Get<bool>("RenderTextures", true);
            RenderText = Config.Get<bool>("RenderText", true);
            RenderWidgets = Config.Get<bool>("RenderWidgets", true);
        }

        public override void Save()
        {
            Config.Add("PreciseMovementSetting", PreciseMovementSetting);
            Config.Add("UseAnchor", UseAnchor);

            Config.Add("RenderTextures", RenderTextures);
            Config.Add("RenderText", RenderText);
            Config.Add("RenderWidgets", RenderWidgets);

            Config.Save();
        }
    }
}