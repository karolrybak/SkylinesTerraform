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
            get { return "Allows terraforming"; }
        }

        public string Name
        {
            get { return "Terraform Tool"; }
        }
    }
    public class LoadingExtension : LoadingExtensionBase
    {

        public InGameTerrainTool buildTool;

        public override void OnLevelLoaded(LoadMode mode)
        {

            try
            {
                GameObject gameController = GameObject.FindWithTag("GameController");
                if (gameController)
                {
                    Log.debug(gameController.ToString());
                    buildTool = gameController.AddComponent<InGameTerrainTool>();

                    Texture2D tex = new Texture2D(64, 64, TextureFormat.ARGB32, false);
                    tex.filterMode = FilterMode.Bilinear;

                    { // LoadTexture
                        System.Reflection.Assembly assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        System.IO.Stream textureStream = assembly.GetManifestResourceStream("TerraformTool.rt_dump_builtin_brush_4.png");

                        byte[] buf = new byte[textureStream.Length];  //declare arraysize
                        textureStream.Read(buf, 0, buf.Length); // read from stream to byte array

                        tex.LoadImage(buf);

                        tex.Apply(true, true);
                    }
                    if(tex != null)
                    {
                        Log.debug("texture loaded");
                    }
                    
                    buildTool.m_brush = tex;
                    UIView v = UIView.GetAView();
                    buildTool.m_mode = TerrainTool.Mode.Level;
                    buildTool.m_brushSize = 25;
                    buildTool.m_strength = 0.5f;

                }
            }
            catch (Exception e)
            {
                Log.debug(e.ToString());
            }


            //buildTool = ToolsModifierControl.toolController.gameObject.GetComponent<TerrainTool>();



            UIView uiView = UIView.GetAView();

            // Add a new button to the view.
            UIButton button = (UIButton)uiView.AddUIComponent(typeof(UIButton));
            // Set the text to show on the button.
            button.text = "Level";
            // Set the button dimensions.
            button.width = 150;
            button.height = 40;
            // Style the button to look like a menu button.
            button.normalBgSprite = "ButtonMenu";
            button.disabledBgSprite = "ButtonMenuDisabled";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.focusedBgSprite = "ButtonMenuFocused";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textColor = new Color32(255, 255, 255, 255);
            button.disabledTextColor = new Color32(7, 7, 7, 255);
            button.hoveredTextColor = new Color32(7, 132, 255, 255);
            button.focusedTextColor = new Color32(255, 255, 255, 255);
            button.pressedTextColor = new Color32(30, 30, 44, 255);
            // Place the button.
            button.transformPosition = new Vector3(-1.65f, 0.97f);
            button.eventClick += toggleQueryTool;

            UIButton button2 = (UIButton)uiView.AddUIComponent(typeof(UIButton));
            // Set the text to show on the button.
            button2.text = "Shift";
            // Set the button2 dimensions.
            button2.width = 150;
            button2.height = 40;
            // Style the button2 to look like a menu button2.
            button2.normalBgSprite = "ButtonMenu";
            button2.disabledBgSprite = "ButtonMenuDisabled";
            button2.hoveredBgSprite = "ButtonMenuHovered";
            button2.focusedBgSprite = "ButtonMenuFocused";
            button2.pressedBgSprite = "ButtonMenuPressed";
            button2.textColor = new Color32(255, 255, 255, 255);
            button2.disabledTextColor = new Color32(7, 7, 7, 255);
            button2.hoveredTextColor = new Color32(7, 132, 255, 255);
            button2.focusedTextColor = new Color32(255, 255, 255, 255);
            button2.pressedTextColor = new Color32(30, 30, 44, 255);
            // Place the button2.
            button2.transformPosition = new Vector3(-1.45f, 0.97f);
            button2.eventClick += toggleQueryTool2;


            if (buildTool != null)
            {
                ToolsModifierControl.toolController.CurrentTool = buildTool;
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, buildTool.ToString());
            }
            else
            {
                DebugOutputPanel.AddMessage(ColossalFramework.Plugins.PluginManager.MessageType.Message, "Failed to init tool");
            }
        }


        void toggleQueryTool2(UIComponent component, UIMouseEventParameter eventParam)
        {
            buildTool.enabled = true;
            buildTool.m_mode = TerrainTool.Mode.Shift;
        }

        void toggleQueryTool(UIComponent component, UIMouseEventParameter eventParam)
        {
            buildTool.enabled = true;
            buildTool.m_mode = TerrainTool.Mode.Level;
        }


    }

}
