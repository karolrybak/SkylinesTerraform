using ColossalFramework;
using ColossalFramework.UI;
using ICities;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace TerraformTool
{
    public class Mod : IUserMod
    {

        public string Description
        {
            get { return "Allows terraforming in game."; }
        }

        public string Name
        {
            get { return "Terraform Tool"; }
        }
    }
    public class LoadingExtension : LoadingExtensionBase
    {

        public InGameTerrainTool buildTool;
        public UITextureAtlas terraform_atlas;

        private UIButton btLevel;
        private UIButton btShift;
        private UIButton btSlope;
        private UIButton btSoften;

        public void LoadResources()
        {
            int spriteWidth = 60;
            int spriteHeight = 41;
            string[] spriteNames = {
                                           "TerrainLevel", 
                                           "TerrainLevelDisabled", 
                                           "TerrainLevelFocused", 
                                           "TerrainLevelHovered", 
                                           "TerrainLevelPressed", 
                                           "TerrainShift", 
                                           "TerrainShiftDisabled", 
                                           "TerrainShiftFocused", 
                                           "TerrainShiftHovered", 
                                           "TerrainShiftPressed", 
                                           "TerrainSlope", 
                                           "TerrainSlopeDisabled", 
                                           "TerrainSlopeFocused", 
                                           "TerrainSlopeHovered", 
                                           "TerrainSlopePressed", 
                                           "TerrainSoften", 
                                           "TerrainSoftenDisabled", 
                                           "TerrainSoftenFocused", 
                                           "TerrainSoftenHovered", 
                                           "TerrainSoftenPressed", 
                                       };

            terraform_atlas = CreateTextureAtlas("spritesheet.png", "TerraformUI", UIView.GetAView().defaultAtlas.material, spriteWidth, spriteHeight, spriteNames);
        }

        UITextureAtlas CreateTextureAtlas(string textureFile, string atlasName, Material baseMaterial, int spriteWidth, int spriteHeight, string[] spriteNames)
        {

            Texture2D tex = new Texture2D(spriteWidth * spriteNames.Length, spriteHeight, TextureFormat.ARGB32, false);
            tex.filterMode = FilterMode.Bilinear;

            { // LoadTexture
                System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                System.IO.Stream textureStream = assembly.GetManifestResourceStream("TerraformTool." + textureFile);

                byte[] buf = new byte[textureStream.Length];  //declare arraysize
                textureStream.Read(buf, 0, buf.Length); // read from stream to byte array

                tex.LoadImage(buf);

                tex.Apply(true, true);
            }

            UITextureAtlas atlas = ScriptableObject.CreateInstance<UITextureAtlas>();

            { // Setup atlas
                Material material = (Material)Material.Instantiate(baseMaterial);
                material.mainTexture = tex;

                atlas.material = material;
                atlas.name = atlasName;
            }

            // Add sprites
            for (int i = 0; i < spriteNames.Length; ++i)
            {
                float uw = 1.0f / spriteNames.Length;

                var spriteInfo = new UITextureAtlas.SpriteInfo()
                {
                    name = spriteNames[i],
                    texture = tex,
                    region = new Rect(i * uw, 0, uw, 1),
                };

                atlas.AddSprite(spriteInfo);
            }

            return atlas;
        }

        public override void OnLevelLoaded(LoadMode mode)
        {
            if (!(mode == LoadMode.LoadGame || mode == LoadMode.NewGame))
            {
                return;
            }
            try
            {
                LoadResources();
                GameObject gameController = GameObject.FindWithTag("GameController");
                if (gameController)
                {
                    //Log.debug(gameController.ToString());
                    buildTool = gameController.AddComponent<InGameTerrainTool>();

                    Texture2D tex = new Texture2D(64, 64, TextureFormat.ARGB32, false);
                    tex.filterMode = FilterMode.Bilinear;

                    { // LoadTexture
                        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        System.IO.Stream textureStream = assembly.GetManifestResourceStream("TerraformTool.builtin_brush_4.png");

                        byte[] buf = new byte[textureStream.Length];  //declare arraysize
                        textureStream.Read(buf, 0, buf.Length); // read from stream to byte array

                        tex.LoadImage(buf);

                        // Do not make read only !
                        tex.Apply();
                    }

                    buildTool.m_brush = tex;
                    UIView v = UIView.GetAView();
                    buildTool.m_mode = TerrainTool.Mode.Level;
                    buildTool.m_brushSize = 25;
                    buildTool.m_strength = 0.5f;
                    buildTool.enabled = false;
                }
            }
            catch (Exception e)
            {
                Log.debug(e.ToString());
            }


            CreateButtons();
        }

        void InitButton(UIButton button, string texture, Vector3 position)
        {
            button.width = 60;
            button.height = 41;
            // Style the button to look like a menu button.
            button.normalBgSprite = texture;
            button.disabledBgSprite = texture + "Disabled";
            button.hoveredBgSprite = texture + "Hovered";
            button.focusedBgSprite = texture + "Focused";
            button.pressedBgSprite = texture + "Pressed";
            // Place the button.
            button.transformPosition = position;
            button.atlas = terraform_atlas;
            button.eventClick += toggleTerraform;
        }

        void CreateButtons()
        {
            UIView uiView = UIView.GetAView();

            btLevel = (UIButton)uiView.AddUIComponent(typeof(UIButton));
            InitButton(btLevel, "TerrainLevel", new Vector3(1.25f, -0.84f));

            btShift = (UIButton)uiView.AddUIComponent(typeof(UIButton));
            InitButton(btShift, "TerrainShift", new Vector3(1.35f, -0.84f));

            btSoften = (UIButton)uiView.AddUIComponent(typeof(UIButton));
            InitButton(btSoften, "TerrainSoften", new Vector3(1.45f, -0.84f));

            btSlope = (UIButton)uiView.AddUIComponent(typeof(UIButton));
            InitButton(btSlope, "TerrainSlope", new Vector3(1.55f, -0.84f));

        }

        void toggleTerraform(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (component == btLevel)
            {                
                buildTool.enabled = true;
                buildTool.m_mode = TerrainTool.Mode.Level;
                buildTool.m_strength = 0.5f;
                buildTool.m_brushSize = 25f;
            }
            if (component == btShift)
            {
                buildTool.enabled = true;
                buildTool.m_mode = TerrainTool.Mode.Shift;
                buildTool.m_strength = 0.01f;
                buildTool.m_brushSize = 25f;
            }
            if (component == btSoften)
            {
                buildTool.enabled = true;
                buildTool.m_mode = TerrainTool.Mode.Soften;
                buildTool.m_strength = 0.02f;
                buildTool.m_brushSize = 50f;
            } 
            if (component == btSlope)
            {
                buildTool.enabled = true;
                buildTool.m_mode = TerrainTool.Mode.Slope;
                buildTool.m_strength = 0.5f;
                buildTool.m_brushSize = 25f;
            }
            
        }


    }

}
