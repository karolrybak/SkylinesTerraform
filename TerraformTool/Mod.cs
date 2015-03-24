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
                    buildTool.m_mode = InGameTerrainTool.Mode.Level;
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

        void InitButton(UIButton button, string texture, int position)
        {
            button.width = 60;
            button.height = 41;
            
            button.normalBgSprite = texture;
            button.disabledBgSprite = texture + "Disabled";
            button.hoveredBgSprite = texture + "Hovered";
            button.focusedBgSprite = texture + "Focused";
            button.pressedBgSprite = texture + "Pressed";
            // Place the button.            
            
            button.atlas = terraform_atlas;
            button.eventClick += toggleTerraform;

            UIView uiView = UIView.GetAView();
            UIComponent refButton = uiView.FindUIComponent("BulldozerButton");

            button.relativePosition = new Vector2
            (
                refButton.relativePosition.x + refButton.width / 2.0f - button.width * position - refButton.width - 8.0f,
                refButton.relativePosition.y + refButton.height / 2.0f - button.height / 2.0f
            );
        }

        void CreateButtons()
        {
            UIView uiView = UIView.GetAView();

            UIComponent refButton = uiView.FindUIComponent("BulldozerButton");

            UIComponent tsBar = uiView.FindUIComponent("TSBar");
            
            if(btLevel == null)
            {
                btLevel = (UIButton)tsBar.AddUIComponent(typeof(UIButton));
                
                InitButton(btLevel, "TerrainLevel", 4);

                btShift = (UIButton)tsBar.AddUIComponent(typeof(UIButton));
                InitButton(btShift, "TerrainShift", 3);

                btSoften = (UIButton)tsBar.AddUIComponent(typeof(UIButton));
                InitButton(btSoften, "TerrainSoften", 2);

                btSlope = (UIButton)tsBar.AddUIComponent(typeof(UIButton));
                InitButton(btSlope, "TerrainSlope", 1);

            }

        }

        void toggleTerraform(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (component == btLevel)
            {                
                buildTool.enabled = true;
                buildTool.m_mode = InGameTerrainTool.Mode.Level;
                buildTool.ApplySettings();
            }
            if (component == btShift)
            {
                buildTool.enabled = true;
                buildTool.m_mode = InGameTerrainTool.Mode.Shift;
                buildTool.ApplySettings();
            }
            if (component == btSoften)
            {
                buildTool.enabled = true;
                buildTool.m_mode = InGameTerrainTool.Mode.Soften;
                buildTool.ApplySettings();
            } 
            if (component == btSlope)
            {
                buildTool.enabled = true;
                buildTool.m_mode = InGameTerrainTool.Mode.Slope;
                buildTool.ApplySettings();
            }
            
        }
    }

}
