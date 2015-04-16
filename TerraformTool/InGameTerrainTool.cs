using ColossalFramework;
using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine;
using ColossalFramework.UI;
using System.Threading;


namespace TerraformTool
{
    public class InGameTerrainTool : ToolBase
    {
        public enum Mode
        {
            Shift,
            Level,
            Soften,
            Slope,
            Point,
            ResourceSand,
        }

        struct UndoStroke
        {
            public int xmin;
            public int xmax;
            public int zmin;
            public int zmax;
            public int pointer;
            public int total_cost;
        }

        struct ToolSettings
        {
            public ToolSettings(float brushSize, float strength)
            {
                m_strength = strength;
                m_brushSize = brushSize;
            }
            public float m_brushSize;
            public float m_strength;

        }

        Dictionary<InGameTerrainTool.Mode, ToolSettings> ModeSettings;

        public UITextureAtlas m_atlas;
        public InGameTerrainTool.Mode m_mode;
        public bool m_free;
        public float m_brushSize = 1f;
        public float m_strength = 0.5f;
        public Texture2D m_brush_circular;
        public Texture2D m_brush_square;
        public CursorInfo m_shiftCursor;
        public CursorInfo m_levelCursor;
        public CursorInfo m_softenCursor;
        public CursorInfo m_slopeCursor;
        Vector3 m_mousePosition;
        Vector3 m_startPosition;
        Vector3 m_endPosition;
        Ray m_mouseRay;
        float m_mouseRayLength;
        bool m_mouseLeftDown;
        bool m_mouseRightDown;
        bool m_mouseRayValid;
        bool m_strokeEnded;
        int m_strokeXmin;
        int m_strokeXmax;
        int m_strokeZmin;
        int m_strokeZmax;
        int m_undoBufferFreePointer;
        List<InGameTerrainTool.UndoStroke> m_undoList;
        bool m_strokeInProgress;
        bool m_undoRequest;
        long m_lastCash;

        UIScrollablePanel terraformPanel;
        UIButton btToggle;
        UIButton btLevel;
        UIButton btShift;
        UIButton btSlope;
        UIButton btSoften;
        UIButton btSand;
        UIButton btPoint;

        object m_dataLock = new object();

        SavedInputKey m_UndoKey = new SavedInputKey(Settings.mapEditorTerrainUndo, Settings.inputSettingsFile, DefaultSettings.mapEditorTerrainUndo, true);
        SavedInputKey m_IncreaseBrushSizeKey = new SavedInputKey(Settings.mapEditorIncreaseBrushSize, Settings.inputSettingsFile, DefaultSettings.mapEditorIncreaseBrushSize, true);
        SavedInputKey m_DecreaseBrushSizeKey = new SavedInputKey(Settings.mapEditorDecreaseBrushSize, Settings.inputSettingsFile, DefaultSettings.mapEditorDecreaseBrushSize, true);
        SavedInputKey m_IncreaseBrushStrengthKey = new SavedInputKey(Settings.mapEditorIncreaseBrushStrength, Settings.inputSettingsFile, DefaultSettings.mapEditorIncreaseBrushStrength, true);
        SavedInputKey m_DecreaseBrushStrengthKey = new SavedInputKey(Settings.mapEditorDecreaseBrushStrength, Settings.inputSettingsFile, DefaultSettings.mapEditorDecreaseBrushStrength, true);

        long m_totalCost;
        const int m_costMultiplier = 50;
        public static ConfigData Config;

        public int m_trenchDepth = 3;
        public int trenchSize = 3;

        readonly ushort[] m_rawHeights = Singleton<TerrainManager>.instance.RawHeights;
        readonly ushort[] m_backupHeights = Singleton<TerrainManager>.instance.BackupHeights;
        readonly ushort[] m_finalHeights = Singleton<TerrainManager>.instance.FinalHeights;
        ushort[] m_undoBuffer = Singleton<TerrainManager>.instance.UndoBuffer;

        public InGameTerrainTool()
        {
            Debug.Log(ConfigData.GetConfigPath());
            if (File.Exists(ConfigData.GetConfigPath()))
            {
                Config = ConfigData.Deserialize();
            }
            else
            {
                Config = new ConfigData();
                ConfigData.Serialize(Config);
            }
            if (InGameTerrainTool.Config != null)
            {
                //m_costMultiplier = InGameTerrainTool.Config.MoneyModifer;
                m_free = InGameTerrainTool.Config.Free;
            }
        }

        void InitButton(UIButton button, string texture, Vector2 size)
        {

            button.normalBgSprite = texture;
            button.disabledBgSprite = texture + "Disabled";
            button.hoveredBgSprite = texture + "Hovered";
            button.focusedBgSprite = texture + "Focused";
            button.pressedBgSprite = texture + "Pressed";
            // Place the button.            

            button.atlas = m_atlas;
            button.eventClick += toggleTerraform;
            button.size = size;
        }


        public void CreateButtons()
        {
            UIView uiView = UIView.GetAView();

            uiView.FindUIComponent ("Policies");
            uiView.FindUIComponent ("Policies");
            if (btLevel == null)
            {
                terraformPanel = UIView.GetAView().FindUIComponent<UITabContainer>("TSContainer").AddUIComponent<UIScrollablePanel>();
                terraformPanel.backgroundSprite = "SubcategoriesPanel";
                terraformPanel.isVisible = false;
                terraformPanel.name = "TerraformPanel";
                terraformPanel.autoLayoutPadding = new RectOffset(25, 0, 20, 20);
                terraformPanel.autoLayout = true;

                btToggle = UIView.GetAView().FindUIComponent<UITabstrip>("MainToolstrip").AddUIComponent<UIButton>();

                InitButton(btToggle, "ToolbarIconTerrain", new Vector2(43, 49));
                btToggle.focusedFgSprite = "ToolbarIconGroup6Focused";
                btToggle.hoveredFgSprite = "ToolbarIconGroup6Hovered";
                btToggle.name = "TerrainButton";

                var btSizeLarge = new Vector2(109, 75);

                btPoint = (UIButton)terraformPanel.AddUIComponent(typeof(UIButton));
                InitButton(btPoint, "TerrainDitch", btSizeLarge);

                
                btShift = (UIButton)terraformPanel.AddUIComponent(typeof(UIButton));
                InitButton(btShift, "TerrainShift", btSizeLarge);

                btSoften = (UIButton)terraformPanel.AddUIComponent(typeof(UIButton));
                InitButton(btSoften, "TerrainSoften", btSizeLarge);

                btLevel = (UIButton)terraformPanel.AddUIComponent(typeof(UIButton));
                InitButton(btLevel, "TerrainLevel", btSizeLarge);

                btSlope = (UIButton)terraformPanel.AddUIComponent(typeof(UIButton));
                InitButton(btSlope, "TerrainSlope", btSizeLarge);

                btSand = (UIButton)terraformPanel.AddUIComponent(typeof(UIButton));
                InitButton(btSand, "ResourceSand", btSizeLarge);




                terraformPanel.Reset();

            }

        }

        void toggleTerraform(UIComponent component, UIMouseEventParameter eventParam)
        {
            component.Focus();
            enabled |= component == btToggle;
            if (component == btLevel)
            {
                enabled = true;
                m_mode = InGameTerrainTool.Mode.Level;
                ApplySettings();
            }
            if (component == btShift)
            {
                enabled = true;
                m_mode = InGameTerrainTool.Mode.Shift;
                ApplySettings();
            }
            if (component == btSoften)
            {
                enabled = true;
                m_mode = InGameTerrainTool.Mode.Soften;
                ApplySettings();
            }
            if (component == btSlope)
            {
                enabled = true;
                m_mode = InGameTerrainTool.Mode.Slope;
                ApplySettings();
            }

            if (component == btSand)
            {
                enabled = true;
                m_mode = InGameTerrainTool.Mode.ResourceSand;
                ApplySettings();
            }

            if (component == btPoint)
            {
                enabled = true;
                m_mode = InGameTerrainTool.Mode.Point;
                ApplySettings();
            }
        }

        public bool IsUndoAvailable()
        {
            return m_undoList != null && m_undoList.Count > 0;
        }
        public void Undo()
        {
            m_undoRequest = true;
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            Monitor.Exit(m_dataLock);

            if (m_mouseRayValid && enabled && m_mode != InGameTerrainTool.Mode.ResourceSand)
            {
                float brushRadius = m_brushSize * 0.5f;

                var color1 = new Color(1.0f, Mathf.Sqrt(m_strength) * 2.5f - 0.77f, 0f);
                Color color2 = Color.yellow;
                var color3 = new Color(0.3f, 0.3f, 0.3f);

                int minX;
                int minZ;
                int maxX;
                int maxZ;
                GetBrushBounds(out minX, out minZ, out maxX, out maxZ, true);

                Singleton<ToolManager>.instance.m_drawCallData.m_overlayCalls++;
                OverlayEffect OverlayEffect = Singleton<RenderManager>.instance.OverlayEffect;

                for (int i = minX - 16; i <= maxX + 16; i += 16)
                {
                    for (int j = minZ - 16; j <= maxZ + 16; j += 16)
                    {
                        if (m_mode == InGameTerrainTool.Mode.Point)
                        {
                            if ((i >= minX && i <= maxX) || (j >= minZ && j <= maxZ))
                            {
                                if (i < minX || i > maxX || j < minZ || j > maxZ)
                                {
                                    OverlayEffect.DrawCircle(cameraInfo, color3, new Vector3(i, 0f, j), 3f, -1f, 1025f, false, true);
                                }
                                else
                                {
                                    OverlayEffect.DrawCircle(cameraInfo, color1, new Vector3(i, 0f, j), 4.5f, -1f, 1025f, false, true);
                                }
                            }
                        }
                        else
                        {
                            float dx = i - m_mousePosition.x;
                            float dz = j - m_mousePosition.z;
                            if (dx * dx + dz * dz < (brushRadius + 1) * (brushRadius + 1))
                            {
                                if (dx * dx + dz * dz > brushRadius * brushRadius)
                                {
                                    OverlayEffect.DrawCircle(cameraInfo, color3, new Vector3(i, 0f, j), 3f, -1f, 1025f, false, true);
                                }
                                else
                                {
                                    OverlayEffect.DrawCircle(cameraInfo, color1, new Vector3(i, 0f, j), 4.5f, -1f, 1025f, false, true);
                                }
                            }
                        }
                    }
                }

                if (m_mode == InGameTerrainTool.Mode.Level || m_mode == InGameTerrainTool.Mode.Slope)
                {
                    if (m_mode == InGameTerrainTool.Mode.Slope)
                    {
                        OverlayEffect.DrawCircle(cameraInfo, color2, m_endPosition, 9f, -1f, 1025f, false, true);
                        if (!m_strokeInProgress)
                        {
                            Vector3 pointerPosition = SnapToTerrain(m_mousePosition);
                            OverlayEffect.DrawCircle(cameraInfo, color2, pointerPosition, 9f, -1f, 1025f, false, true);
                        }
                    }
                    OverlayEffect.DrawCircle(cameraInfo, color2, m_startPosition, 9f, -1f, 1025f, false, true);
                }
            }

            base.RenderOverlay(cameraInfo);
        }

        public void ResetUndoBuffer()
        {
            m_undoList.Clear();

            for (int i = 0; i <= 1080; i++)
            {
                for (int j = 0; j <= 1080; j++)
                {
                    int num = i * 1081 + j;
                    m_backupHeights[num] = m_rawHeights[num];
                }
            }
        }
        protected override void Awake()
        {
            base.Awake();
            //changed default brush settings here  @SimsFirehouse
            ModeSettings = new Dictionary<Mode, ToolSettings>();
            ModeSettings[Mode.Level] = new ToolSettings(24, 0.5f);
            ModeSettings[Mode.Shift] = new ToolSettings(24, 0.1f);
            ModeSettings[Mode.Soften] = new ToolSettings(48, 0.1f);
            ModeSettings[Mode.Slope] = new ToolSettings(24, 0.5f);
            ModeSettings[Mode.ResourceSand] = new ToolSettings(48, 0.5f);
            ModeSettings[Mode.Point] = new ToolSettings(24, 0.4f);

            m_undoList = new List<InGameTerrainTool.UndoStroke>();
            if (Singleton<LoadingManager>.exists)
            {
				Singleton<LoadingManager>.instance.m_levelLoaded += OnLevelLoaded;
            }
        }
        public void ApplySettings()
        {
            m_strength = ModeSettings[m_mode].m_strength;
            m_brushSize = ModeSettings[m_mode].m_brushSize;
        }

        void UpdateSettings()
        {
            ModeSettings[m_mode] = new ToolSettings(m_brushSize, m_strength);
        }

        protected override void OnToolGUI()
        {
            Event current = Event.current;

            if (!m_toolController.IsInsideUI && current.type == EventType.MouseDown)
            {
                m_lastCash = EconomyManager.instance.LastCashAmount;
                if (current.button == 0)
                {
                    m_mouseLeftDown = true;
                    if (m_mode == InGameTerrainTool.Mode.Slope)
                    {
                        m_endPosition = SnapToTerrain(m_mousePosition);
                    }
                }
                else if (current.button == 1)
                {
                    if (m_mode == InGameTerrainTool.Mode.Shift || m_mode == InGameTerrainTool.Mode.Point || m_mode == InGameTerrainTool.Mode.Soften)
                    {
                        m_mouseRightDown = true;
                    }
                    else if (m_mode == InGameTerrainTool.Mode.Level || m_mode == InGameTerrainTool.Mode.Slope)
                    {
                        m_startPosition = SnapToTerrain(m_mousePosition);
                    }
                }
            }
            else if (current.type == EventType.MouseUp)
            {
                if (current.button == 0)
                {
                    m_mouseLeftDown = false;
                    m_strokeEnded |= !m_mouseRightDown;
                }
                else if (current.button == 1)
                {
                    m_mouseRightDown = false;
                    m_strokeEnded |= !m_mouseLeftDown;
                }
            }
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape && !m_undoRequest && !m_mouseLeftDown && !m_mouseRightDown)
            {
                current.Use();
                enabled = false;
            }
            if (m_UndoKey.IsPressed(current) && !m_undoRequest && !m_mouseLeftDown && !m_mouseRightDown && IsUndoAvailable())
            {
                Undo();
            }
            //changed brush setting here  @SimsFirehouse
            if (m_IncreaseBrushSizeKey.IsPressed(current))
            {
                m_brushSize = Mathf.Min(320, m_brushSize + 8);
                UpdateSettings();
            }
            if (m_DecreaseBrushSizeKey.IsPressed(current))
            {
                m_brushSize = Mathf.Max(16, m_brushSize - 8);
                UpdateSettings();
            }
            if (m_IncreaseBrushStrengthKey.IsPressed(current))
            {
                m_strength = Mathf.Min(0.5f, m_strength + 0.1f);
                UpdateSettings();
            }
            if (m_DecreaseBrushStrengthKey.IsPressed(current))
            {
                m_strength = Mathf.Max(0.1f, m_strength - 0.1f);
                UpdateSettings();
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_mode == InGameTerrainTool.Mode.Point)
            {
                m_toolController.SetBrush(m_brush_square, m_mousePosition, m_brushSize);
            }
            else
            {
                m_toolController.SetBrush(m_brush_circular, m_mousePosition, m_brushSize);
            }
            m_strokeXmin = 1080;
            m_strokeXmax = 0;
            m_strokeZmin = 1080;
            m_strokeZmax = 0;
            for (int i = 0; i <= 1080; i++)
            {
                for (int j = 0; j <= 1080; j++)
                {
                    int num = i * 1081 + j;
                    m_backupHeights[num] = m_rawHeights[num];
                }
            }
            TerrainManager.instance.TransparentWater = true;
        }

        void OnLevelLoaded(SimulationManager.UpdateMode mode)
        {
            ResetUndoBuffer();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            ToolCursor = null;
            m_toolController.SetBrush(null, Vector3.zero, 1f);
            m_mouseLeftDown = false;
            m_mouseRightDown = false;
            m_mouseRayValid = false;
            Singleton<TerrainManager>.instance.TransparentWater = false;
            ResetUndoBuffer();
            terraformPanel.isVisible = false;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Singleton<LoadingManager>.exists)
            {
				Singleton<LoadingManager>.instance.m_levelLoaded -= OnLevelLoaded;
            }
        }

        protected override void OnToolUpdate()
        {
            switch (m_mode)
            {
                case InGameTerrainTool.Mode.Shift:
                    ToolCursor = m_shiftCursor;
                    break;
                case InGameTerrainTool.Mode.Level:
                    ToolCursor = m_levelCursor;
                    break;
                case InGameTerrainTool.Mode.Soften:
                    ToolCursor = m_softenCursor;
                    break;
                case InGameTerrainTool.Mode.Slope:
                    ToolCursor = m_slopeCursor;
                    break;
            }
        }

        protected override void OnToolLateUpdate()
        {
			m_free = EconomyManager.instance.LastCashAmount == Int64.MaxValue || Config.Free;

            Vector3 mousePosition = Input.mousePosition;
            m_mouseRay = Camera.main.ScreenPointToRay(mousePosition);
            m_mouseRayLength = Camera.main.farClipPlane;
            m_mouseRayValid = (!m_toolController.IsInsideUI && Cursor.visible);
            if (m_mode == InGameTerrainTool.Mode.Point)
            {
                m_toolController.SetBrush(m_brush_square, m_mousePosition, m_brushSize);
            }
            else
            {
                m_toolController.SetBrush(m_brush_circular, m_mousePosition, m_brushSize);
            }
        }

        public override void SimulationStep()
        {
            var input = new ToolBase.RaycastInput(m_mouseRay, m_mouseRayLength);
            ToolBase.RaycastOutput raycastOutput;
            if (m_undoRequest && !m_strokeInProgress)
            {
                ApplyUndo();
                m_undoRequest = false;
            }
            else if (m_strokeEnded)
            {
                EndStroke();
                m_strokeEnded = false;
                m_strokeInProgress = false;
            }
            else if (m_mouseRayValid && ToolBase.RayCast(input, out raycastOutput))
            {
                m_mousePosition = raycastOutput.m_hitPos;
                if (m_mouseLeftDown != m_mouseRightDown)
                {
                    if (m_mode == Mode.ResourceSand)
                    {
                        ApplyBrushResource(!m_mouseLeftDown);
                    }
                    else
                    {
                        //merged ApplyPoint() into ApplyBrush()  @SimsFirehouse
                        ApplyBrush();
                        m_strokeInProgress = true;
                    }
                }
            }
        }

        int GetFreeUndoSpace()
        {
            int num = m_undoBuffer.Length;
            if (m_undoList.Count > 0)
            {
                return (num + m_undoList[0].pointer - m_undoBufferFreePointer) % num - 1;
            }
            return num - 1;
        }

        void EndStroke()
        {
            int num2 = Math.Max(0, 1 + m_strokeXmax - m_strokeXmin) * Math.Max(0, 1 + m_strokeZmax - m_strokeZmin);
            if (num2 < 1)
            {
                return;
            }
            int num3 = 0;
            while (GetFreeUndoSpace() < num2 && num3 < 10000)
            {
                m_undoList.RemoveAt(0);
                num3++;
            }
            if (num3 >= 10000)
            {
                Debug.Log("InGameTerrainTool:EndStroke: unexpectedly terminated freeing loop, might be a bug.");
                return;
            }
            InGameTerrainTool.UndoStroke item = default(InGameTerrainTool.UndoStroke);
            item.xmin = m_strokeXmin;
            item.xmax = m_strokeXmax;
            item.zmin = m_strokeZmin;
            item.zmax = m_strokeZmax;
            item.total_cost = (int)m_totalCost;
            item.pointer = m_undoBufferFreePointer;
            m_undoList.Add(item);
            for (int i = m_strokeZmin; i <= m_strokeZmax; i++)
            {
                for (int j = m_strokeXmin; j <= m_strokeXmax; j++)
                {
                    int num4 = i * 1081 + j;
                    m_undoBuffer[m_undoBufferFreePointer++] = m_backupHeights[num4];
                    m_backupHeights[num4] = m_rawHeights[num4];
                    m_undoBufferFreePointer %= m_undoBuffer.Length;
                }
            }
            m_strokeXmin = 1080;
            m_strokeXmax = 0;
            m_strokeZmin = 1080;
            m_strokeZmax = 0;
            m_totalCost = 0;
        }

        public void ApplyUndo()
        {
            if (m_undoList.Count < 1)
            {
                return;
            }
            InGameTerrainTool.UndoStroke undoStroke = m_undoList[m_undoList.Count - 1];
            m_undoList.RemoveAt(m_undoList.Count - 1);

            int Xmin = undoStroke.xmin;
            int Xmax = undoStroke.xmax;
            int Zmin = undoStroke.zmin;
            int Zmax = undoStroke.zmax;
            int pointer = undoStroke.pointer;
            int num = m_undoBuffer.Length;
            int num3 = undoStroke.pointer;
            for (int i = undoStroke.zmin; i <= undoStroke.zmax; i++)
            {
                for (int j = undoStroke.xmin; j <= undoStroke.xmax; j++)
                {
                    int num4 = i * 1081 + j;
                    m_rawHeights[num4] = m_undoBuffer[num3];
                    m_backupHeights[num4] = m_undoBuffer[num3];
                    num3++;
                    num3 %= num;
                }
            }
            m_undoBufferFreePointer = undoStroke.pointer;

            m_strokeXmin = 1080;
            m_strokeXmax = 0;
            m_strokeZmin = 1080;
            m_strokeZmax = 0;

            TerrainModify.UpdateArea(Xmin - 1, Zmin - 1, Xmax + 1, Zmax + 1, true, false, false);
			if (!m_free) {
				EconomyManager.instance.FetchResource (EconomyManager.Resource.Construction, -undoStroke.total_cost, ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Level.None);
			}

        }

        void ApplyBrushResource(bool negate)
        {
            float[] brushData = m_toolController.BrushData;
            float num = m_brushSize * 0.5f;
            NaturalResourceManager.ResourceCell[] naturalResources = Singleton<NaturalResourceManager>.instance.m_naturalResources;
            float strength = m_strength;
            Vector3 mousePosition = m_mousePosition;
            int num4 = Mathf.Max((int)((mousePosition.x - num) / 33.75f + (float)512 * 0.5f), 0);
            int num5 = Mathf.Max((int)((mousePosition.z - num) / 33.75f + (float)512 * 0.5f), 0);
            int num6 = Mathf.Min((int)((mousePosition.x + num) / 33.75f + (float)512 * 0.5f), 512 - 1);
            int num7 = Mathf.Min((int)((mousePosition.z + num) / 33.75f + (float)512 * 0.5f), 512 - 1);
            for (int i = num5; i <= num7; i++)
            {
                float num8 = (((float)i - (float)512 * 0.5f + 0.5f) * 33.75f - mousePosition.z + num) / m_brushSize * 64f - 0.5f;
                int num9 = Mathf.Clamp(Mathf.FloorToInt(num8), 0, 63);
                int num10 = Mathf.Clamp(Mathf.CeilToInt(num8), 0, 63);
                for (int j = num4; j <= num6; j++)
                {
                    float num11 = (((float)j - (float)512 * 0.5f + 0.5f) * 33.75f - mousePosition.x + num) / m_brushSize * 64f - 0.5f;
                    int num12 = Mathf.Clamp(Mathf.FloorToInt(num11), 0, 63);
                    int num13 = Mathf.Clamp(Mathf.CeilToInt(num11), 0, 63);
                    float num14 = brushData[num9 * 64 + num12];
                    float num15 = brushData[num9 * 64 + num13];
                    float num16 = brushData[num10 * 64 + num12];
                    float num17 = brushData[num10 * 64 + num13];
                    float num18 = num14 + (num15 - num14) * (num11 - (float)num12);
                    float num19 = num16 + (num17 - num16) * (num11 - (float)num12);
                    float num20 = num18 + (num19 - num18) * (num8 - (float)num9);
                    NaturalResourceManager.ResourceCell resourceCell = naturalResources[i * 512 + j];
                    int num21 = (int)(255f * strength * num20);
                    if (negate)
                    {
                        num21 = -num21;
                    }

                    ChangeMaterial(ref resourceCell.m_sand, ref resourceCell.m_fertility, num21);

                    naturalResources[i * 512 + j] = resourceCell;
                }
            }
            Singleton<NaturalResourceManager>.instance.AreaModified(num4, num5, num6, num7);
        }

        static void ChangeMaterial(ref byte amount, ref byte other, int change)
        {
            if (change > 0)
            {
                if ((int)other >= change)
                {
                    other = (byte)((int)other - change);
                }
                else
                {
                    amount = (byte)Mathf.Min((int)amount + change - (int)other, 255);
                    other = 0;
                }
            }
            else
            {
                amount = (byte)Mathf.Max((int)amount + change, 0);
            }
        }

        static Vector3 SnapToTerrain(Vector3 mouse)
        {
            return new Vector3(Mathf.RoundToInt(mouse.x / 16f), 0f, Mathf.RoundToInt(mouse.z / 16f)) * 16f;
        }

        static float ConvertCoords(float coords, bool ScreenToTerrain = true)
        {
            return ScreenToTerrain ? coords / 16f + 1080 / 2 : (coords - 1080 / 2) * 16f;
        }

        Vector3 ConvertCoords(Vector3 Pos, bool ScreenToTerrain = true)
        {
            return new Vector3
            {
                x = ConvertCoords(Pos.x, ScreenToTerrain),
                z = ConvertCoords(Pos.z, ScreenToTerrain)
            };
        }

        void GetBrushBounds(out int minX, out int minZ, out int maxX, out int maxZ, bool screenPos = false)
        {
            float brushRadius = m_brushSize / 2;
            minX = Mathf.Max(Mathf.CeilToInt(ConvertCoords(m_mousePosition.x - brushRadius)), 1);
            minZ = Mathf.Max(Mathf.CeilToInt(ConvertCoords(m_mousePosition.z - brushRadius)), 1);
            maxX = Mathf.Min(Mathf.FloorToInt(ConvertCoords(m_mousePosition.x + brushRadius)), 1080 - 1);
            maxZ = Mathf.Min(Mathf.FloorToInt(ConvertCoords(m_mousePosition.z + brushRadius)), 1080 - 1);
            if (screenPos)
            {
                minX = (int)ConvertCoords(minX, false);
                minZ = (int)ConvertCoords(minZ, false);
                maxX = (int)ConvertCoords(maxX, false);
                maxZ = (int)ConvertCoords(maxZ, false);
            }
        }
        //merged ApplyPoint() into ApplyBrush()  @SimsFirehouse

        void ApplyBrush()
        {
            long m_applyCost = 0;
            bool outOfMoney = false;
            bool applied = false;
            int minX;
            int minZ;
            int maxX;
            int maxZ;
            GetBrushBounds(out minX, out minZ, out maxX, out maxZ);
            int originalHeight;
            ushort endHeight = 0;
            int smoothStrength = m_mouseRightDown ? (int)(m_strength * 20 + 2) : (int)(m_strength * 10 + 1);
            //2-6 when left click, 4-12 when right click

			if(!m_strokeInProgress)
            m_startPosition.y = m_rawHeights[(int)ConvertCoords(m_startPosition.z) * 1081 + (int)ConvertCoords(m_startPosition.x)] / 64;

            for (int i = minZ; i <= maxZ; i++)
            {
                for (int j = minX; j <= maxX; j++)
                {
                    if (m_mode == InGameTerrainTool.Mode.Point)
                    {
                        int elevationStep = (int)(m_strength * 10);
                        int diff = m_trenchDepth * 64 * elevationStep * (m_mouseLeftDown ? 1 : -1);
                        originalHeight = (int)m_backupHeights[i * 1081 + j];
                        endHeight = (ushort)Mathf.Clamp(originalHeight + diff, 0, 65535);
                    }
                    else
                    {
                        Vector3 position = ConvertCoords(new Vector3(j, 0f, i), false);
                        Vector3 mousePos = m_mousePosition;
                        mousePos.y = 0f;
                        float brushRadius = m_brushSize * 0.5f;
                        float targetHeight = 0f;
                        float t1 = Mathf.Clamp(1 - (position - mousePos).sqrMagnitude / (brushRadius * brushRadius), 0f, 1f);
                        originalHeight = (int)m_rawHeights[i * 1081 + j];
                        if (m_mode == InGameTerrainTool.Mode.Shift)
                        {
                            targetHeight = originalHeight + m_trenchDepth * 64 * (m_mouseLeftDown ? 1 : -1);
                        }
                        else if (m_mode == InGameTerrainTool.Mode.Level)
                        {
                            targetHeight = m_startPosition.y * 64;
                        }
                        else if (m_mode == InGameTerrainTool.Mode.Soften)
                        {
                            int minJ = Mathf.Max(j - smoothStrength, 0);
                            int minI = Mathf.Max(i - smoothStrength, 0);
                            int maxJ = Mathf.Min(j + smoothStrength, 1080);
                            int maxI = Mathf.Min(i + smoothStrength, 1080);
                            float area = 0f;
                            for (int k = minI; k <= maxI; k++)
                            {
                                for (int l = minJ; l <= maxJ; l++)
                                {
                                    float t3 = 1f - ((l - j) * (l - j) + (k - i) * (k - i)) / (smoothStrength * smoothStrength);
                                    if (t3 > 0f)
                                    {
                                        targetHeight += (float)m_finalHeights[k * 1081 + l] * t3;
                                        area += t3;
                                    }
                                }
                            }
                            targetHeight /= area;
                        }
                        else if (m_mode == InGameTerrainTool.Mode.Slope)
                        {
                            Vector3 vector = m_endPosition - m_startPosition;
                            vector.y = 0f;
                            Vector3 startPos = m_startPosition;
                            startPos.y = 0f;
                            float t2 = Mathf.Clamp(Vector3.Dot(position - startPos, vector) / vector.sqrMagnitude, 0f, 1f);
                            targetHeight = Mathf.Lerp(m_startPosition.y, m_endPosition.y, t2) * 64;
                        }
                        targetHeight = Mathf.Clamp(targetHeight, 0, 65535);
                        endHeight = (ushort)Mathf.Lerp(originalHeight, targetHeight, m_strength * t1);
                    }
                    if (!outOfMoney)
                        m_applyCost += Mathf.Abs(endHeight - originalHeight) * m_costMultiplier;

                    if ((m_applyCost + m_totalCost < m_lastCash && m_applyCost + m_totalCost < Int32.MaxValue) || m_free)
                    {
                        m_rawHeights[i * 1081 + j] = endHeight;
                        m_strokeXmin = Math.Min(m_strokeXmin, j);
                        m_strokeXmax = Math.Max(m_strokeXmax, j);
                        m_strokeZmin = Math.Min(m_strokeZmin, i);
                        m_strokeZmax = Math.Max(m_strokeZmax, i);
                        applied = true;
                    }
                    else
                    {
                        outOfMoney = true;
                    }

                }
            }
            TerrainModify.UpdateArea(minX - 1, minZ - 1, maxX + 1, maxZ + 1, true, true, false);

            if (applied)
            {
				if (!m_free) {
					EconomyManager.instance.FetchResource (EconomyManager.Resource.Construction, (int)m_applyCost, ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Level.None);
					m_totalCost += m_applyCost;
				}
            }
        }

    }



}