using ColossalFramework.IO;
using ColossalFramework;
using System.IO;
using System;
using System.Collections.Generic;
using UnityEngine;
using ColossalFramework.UI;
using ColossalFramework.Math;
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

        private struct UndoStroke
        {
            public int xmin;
            public int xmax;
            public int zmin;
            public int zmax;
            public int pointer;
            public int total_cost;
        }

        private struct ToolSettings
        {
            public ToolSettings(float m_brushSize, float m_strength)
            {
                this.m_strength = m_strength;
                this.m_brushSize = m_brushSize;
            }
            public float m_brushSize;
            public float m_strength;

        }

        private Dictionary<InGameTerrainTool.Mode, ToolSettings> ModeSettings;

        public UITextureAtlas m_atlas;
        public InGameTerrainTool.Mode m_mode;
        public bool m_free = false;
        public float m_brushSize = 1f;
        public float m_strength = 0.5f;
        public Texture2D m_brush;
        public CursorInfo m_shiftCursor;
        public CursorInfo m_levelCursor;
        public CursorInfo m_softenCursor;
        public CursorInfo m_slopeCursor;
        private Vector3 m_mousePosition;
        internal Vector3 m_startPosition;
        private Vector3 m_endPosition;
        private Ray m_mouseRay;
        private float m_mouseRayLength;
        private bool m_mouseLeftDown;
        private bool m_mouseRightDown;
        private bool m_mouseRayValid;
        private bool m_strokeEnded;
        private int m_strokeXmin;
        private int m_strokeXmax;
        private int m_strokeZmin;
        private int m_strokeZmax;
        private int m_undoBufferFreePointer;
        private List<InGameTerrainTool.UndoStroke> m_undoList;
        private bool m_strokeInProgress;
        private bool m_undoRequest;
        private long m_lastCash;

        UIScrollablePanel terraformPanel;
        private UIButton btToggle;
        private UIButton btLevel;
        private UIButton btShift;
        private UIButton btSlope;
        private UIButton btSoften;
        private UIButton btSand;
        private UIButton btPoint;

        private object m_dataLock = new object();

        private SavedInputKey m_UndoKey = new SavedInputKey(Settings.mapEditorTerrainUndo, Settings.inputSettingsFile, DefaultSettings.mapEditorTerrainUndo, true);
        private SavedInputKey m_IncreaseBrushSizeKey = new SavedInputKey(Settings.mapEditorIncreaseBrushSize, Settings.inputSettingsFile, DefaultSettings.mapEditorIncreaseBrushSize, true);
        private SavedInputKey m_DecreaseBrushSizeKey = new SavedInputKey(Settings.mapEditorDecreaseBrushSize, Settings.inputSettingsFile, DefaultSettings.mapEditorDecreaseBrushSize, true);
        private SavedInputKey m_IncreaseBrushStrengthKey = new SavedInputKey(Settings.mapEditorIncreaseBrushStrength, Settings.inputSettingsFile, DefaultSettings.mapEditorIncreaseBrushStrength, true);
        private SavedInputKey m_DecreaseBrushStrengthKey = new SavedInputKey(Settings.mapEditorDecreaseBrushStrength, Settings.inputSettingsFile, DefaultSettings.mapEditorDecreaseBrushStrength, true);

        private long m_totalCost;
        private int m_costMultiplier = 50;
        public static ConfigData Config;

        public int trenchDepth = 750;
        public int trenchSize = 3;

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
                //this.m_costMultiplier = InGameTerrainTool.Config.MoneyModifer;
                this.m_free = InGameTerrainTool.Config.Free;
            }
        }

        void InitButton(UIButton button, string texture, Vector2 size)
        {

            button.normalBgSprite = texture;
            button.disabledBgSprite = texture + "Disabled";
            button.hoveredBgSprite = texture + "Focused";
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

            UIComponent refButton = uiView.FindUIComponent("Policies");
            UIComponent tsBar = uiView.FindUIComponent("TSBar");
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
            if (component == btToggle)
            {
                enabled = true;
            }
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
            return this.m_undoList != null && this.m_undoList.Count > 0;
        }
        public void Undo()
        {
            this.m_undoRequest = true;
        }

        public override void RenderOverlay(RenderManager.CameraInfo cameraInfo)
        {
            while (!Monitor.TryEnter(this.m_dataLock, SimulationManager.SYNCHRONIZE_TIMEOUT))
            {
            }
            Vector3 startPosition;
            Vector3 mousePosition;
            try
            {
                startPosition = this.m_startPosition;
                mousePosition = this.m_mousePosition;
            }
            finally
            {
                Monitor.Exit(this.m_dataLock);
            }

            var color = Color.red;

            if (m_mode != Mode.Point)
            {
                base.RenderOverlay(cameraInfo);
                return;
            }

            Vector3 a = mousePosition;
            Vector3 vector = mousePosition;

            //a.x = (int)((mousePosition.x) / TerrainManager.RAW_CELL_SIZE) * TerrainManager.RAW_CELL_SIZE;
            //a.z = (int)((mousePosition.z) / TerrainManager.RAW_CELL_SIZE) * TerrainManager.RAW_CELL_SIZE;

            Vector3 a2 = Vector3.forward;
            Vector3 a3 = new Vector3(a2.z, 0f, -a2.x);

            float num = Mathf.Round(((vector.x - a.x) * a2.x + (vector.z - a.z) * a2.z) * 0.125f) * 8f;
            float num2 = Mathf.Round(((vector.x - a.x) * a3.x + (vector.z - a.z) * a3.z) * 0.125f) * 8f;

            float num3 = (num < 0f) ? -4f : 4f;
            float num4 = (num2 < 0f) ? -4f : 4f;

            Quad3 quad = default(Quad3);
            quad.a = a - a2 * num3 - a3 * num4;
            quad.b = a - a2 * num3 + a3 * (num2 + num4);
            quad.c = a + a2 * (num + num3) + a3 * (num2 + num4);
            quad.d = a + a2 * (num + num3) - a3 * num4;

            if (num3 != num4)
            {
                Vector3 b = quad.b;
                quad.b = quad.d;
                quad.d = b;
            }
            ToolManager toolManager = ToolManager.instance;
            toolManager.m_drawCallData.m_overlayCalls++;
            RenderManager.instance.OverlayEffect.DrawQuad(cameraInfo, color, quad, -1f, 1025f, false, true);

            base.RenderOverlay(cameraInfo);
        }

        public void ResetUndoBuffer()
        {
            this.m_undoList.Clear();

            ushort[] backupHeights = Singleton<TerrainManager>.instance.BackupHeights;
            ushort[] rawHeights = Singleton<TerrainManager>.instance.RawHeights;
            for (int i = 0; i <= 1080; i++)
            {
                for (int j = 0; j <= 1080; j++)
                {
                    int num = i * 1081 + j;
                    backupHeights[num] = rawHeights[num];
                }
            }
        }
        protected override void Awake()
        {
            base.Awake();
            ModeSettings = new Dictionary<Mode, ToolSettings>();
            ModeSettings[Mode.Level] = new ToolSettings(25, 0.5f);
            ModeSettings[Mode.Shift] = new ToolSettings(25, 0.01f);
            ModeSettings[Mode.Soften] = new ToolSettings(50, 0.2f);
            ModeSettings[Mode.Slope] = new ToolSettings(25, 0.5f);
            ModeSettings[Mode.ResourceSand] = new ToolSettings(50, 0.5f);
            ModeSettings[Mode.Point] = new ToolSettings(0, 0.5f);

            this.m_undoList = new List<InGameTerrainTool.UndoStroke>();
            if (Singleton<LoadingManager>.exists)
            {
                Singleton<LoadingManager>.instance.m_levelLoaded += new LoadingManager.LevelLoadedHandler(this.OnLevelLoaded);
            }
        }
        public void ApplySettings()
        {
            this.m_strength = ModeSettings[m_mode].m_strength;
            this.m_brushSize = ModeSettings[m_mode].m_brushSize;
        }

        private void UpdateSettings()
        {
            ModeSettings[this.m_mode] = new ToolSettings(m_brushSize, m_strength);
        }

        protected override void OnToolGUI()
        {
            Event current = Event.current;

            if (!this.m_toolController.IsInsideUI && current.type == EventType.MouseDown)
            {
                m_lastCash = EconomyManager.instance.LastCashAmount;
                if (current.button == 0)
                {
                    this.m_mouseLeftDown = true;
                    this.m_endPosition = this.m_mousePosition;
                }
                else if (current.button == 1)
                {
                    if (this.m_mode == InGameTerrainTool.Mode.Shift || this.m_mode == InGameTerrainTool.Mode.Soften || this.m_mode == InGameTerrainTool.Mode.ResourceSand || this.m_mode == InGameTerrainTool.Mode.Point)
                    {
                        this.m_mouseRightDown = true;
                    }
                    else if (this.m_mode == InGameTerrainTool.Mode.Level || this.m_mode == InGameTerrainTool.Mode.Slope)
                    {
                        this.m_startPosition = this.m_mousePosition;
                    }
                }
            }
            else if (current.type == EventType.MouseUp)
            {
                if (current.button == 0)
                {
                    this.m_mouseLeftDown = false;
                    if (!this.m_mouseRightDown)
                    {
                        this.m_strokeEnded = true;
                    }
                }
                else if (current.button == 1)
                {
                    this.m_mouseRightDown = false;
                    if (!this.m_mouseLeftDown)
                    {
                        this.m_strokeEnded = true;
                    }
                }
            }
            if (current.type == EventType.KeyDown && current.keyCode == KeyCode.Escape && !this.m_undoRequest && !this.m_mouseLeftDown && !this.m_mouseRightDown)
            {
                current.Use();
                this.enabled = false;
            }
            if (this.m_UndoKey.IsPressed(current) && !this.m_undoRequest && !this.m_mouseLeftDown && !this.m_mouseRightDown && this.IsUndoAvailable())
            {
                this.Undo();
            }
            if(m_mode != Mode.Point)
            {
                if (this.m_IncreaseBrushSizeKey.IsPressed(current) && !this.m_undoRequest && !this.m_mouseLeftDown && !this.m_mouseRightDown)
                {
                    m_brushSize = Mathf.Min(1250, m_brushSize + 5);
                    UpdateSettings();
                }
                if (this.m_DecreaseBrushSizeKey.IsPressed(current) && !this.m_undoRequest && !this.m_mouseLeftDown && !this.m_mouseRightDown)
                {
                    m_brushSize = Mathf.Max(25, m_brushSize - 5);
                    UpdateSettings();
                }
                if (this.m_IncreaseBrushStrengthKey.IsPressed(current) && !this.m_undoRequest && !this.m_mouseLeftDown && !this.m_mouseRightDown)
                {
                    m_strength = Mathf.Min(1, m_strength + 0.05f);
                    UpdateSettings();
                }
                if (this.m_DecreaseBrushStrengthKey.IsPressed(current) && !this.m_undoRequest && !this.m_mouseLeftDown && !this.m_mouseRightDown)
                {
                    m_strength = Mathf.Max(0.01f, m_strength - 0.05f);
                    UpdateSettings();
                }
            }
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            this.m_toolController.SetBrush(this.m_brush, this.m_mousePosition, this.m_brushSize);
            this.m_strokeXmin = 1080;
            this.m_strokeXmax = 0;
            this.m_strokeZmin = 1080;
            this.m_strokeZmax = 0;
            ushort[] backupHeights = TerrainManager.instance.BackupHeights;
            ushort[] rawHeights = TerrainManager.instance.RawHeights;
            for (int i = 0; i <= 1080; i++)
            {
                for (int j = 0; j <= 1080; j++)
                {
                    int num = i * 1081 + j;
                    backupHeights[num] = rawHeights[num];
                }
            }
            TerrainManager.instance.TransparentWater = true;
        }

        private void OnLevelLoaded(SimulationManager.UpdateMode mode)
        {
            this.ResetUndoBuffer();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            base.ToolCursor = null;
            this.m_toolController.SetBrush(null, Vector3.zero, 1f);
            this.m_mouseLeftDown = false;
            this.m_mouseRightDown = false;
            this.m_mouseRayValid = false;
            Singleton<TerrainManager>.instance.TransparentWater = false;
            ResetUndoBuffer();
            terraformPanel.isVisible = false;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Singleton<LoadingManager>.exists)
            {
                Singleton<LoadingManager>.instance.m_levelLoaded -= new LoadingManager.LevelLoadedHandler(this.OnLevelLoaded);
            }
        }

        protected override void OnToolUpdate()
        {
            switch (this.m_mode)
            {
                case InGameTerrainTool.Mode.Shift:
                    base.ToolCursor = this.m_shiftCursor;
                    break;
                case InGameTerrainTool.Mode.Level:
                    base.ToolCursor = this.m_levelCursor;
                    break;
                case InGameTerrainTool.Mode.Soften:
                    base.ToolCursor = this.m_softenCursor;
                    break;
                case InGameTerrainTool.Mode.Slope:
                    base.ToolCursor = this.m_slopeCursor;
                    break;
            }
        }

        protected override void OnToolLateUpdate()
        {
            if (EconomyManager.instance.LastCashAmount == Int64.MaxValue)
            {
                m_free = true;
            }
            else
            {
                m_free = Config.Free;
            }

            Vector3 mousePosition = Input.mousePosition;
            this.m_mouseRay = Camera.main.ScreenPointToRay(mousePosition);
            this.m_mouseRayLength = Camera.main.farClipPlane;
            this.m_mouseRayValid = (!this.m_toolController.IsInsideUI && Cursor.visible);
            this.m_toolController.SetBrush(this.m_brush, this.m_mousePosition, this.m_brushSize);
        }

        public override void SimulationStep()
        {
            ToolBase.RaycastInput input = new ToolBase.RaycastInput(this.m_mouseRay, this.m_mouseRayLength);
            ToolBase.RaycastOutput raycastOutput;
            if (this.m_undoRequest && !this.m_strokeInProgress)
            {
                this.ApplyUndo();
                this.m_undoRequest = false;
            }
            else if (this.m_strokeEnded)
            {
                this.EndStroke();
                this.m_strokeEnded = false;
                this.m_strokeInProgress = false;
            }
            else if (this.m_mouseRayValid && ToolBase.RayCast(input, out raycastOutput))
            {
                this.m_mousePosition = raycastOutput.m_hitPos;
                if (this.m_mouseLeftDown != this.m_mouseRightDown)
                {
                    if (this.m_mode == Mode.ResourceSand)
                    {
                        this.ApplyBrushResource(!m_mouseLeftDown);
                    }
                    else if (m_mode == Mode.Point)
                    {
                        //deleted parameter input  @SimsFirehouse
                        ApplyPoint();
                        this.m_strokeInProgress = true;
                    }
                    else
                    {
                        this.m_strokeInProgress = true;
                        ApplyBrush();
                    }

                }
            }
        }

        private int GetFreeUndoSpace()
        {
            int num = Singleton<TerrainManager>.instance.UndoBuffer.Length;
            if (this.m_undoList.Count > 0)
            {
                return (num + this.m_undoList[0].pointer - this.m_undoBufferFreePointer) % num - 1;
            }
            return num - 1;
        }

        private void EndStroke()
        {
            int num = Singleton<TerrainManager>.instance.UndoBuffer.Length;
            int num2 = Math.Max(0, 1 + this.m_strokeXmax - this.m_strokeXmin) * Math.Max(0, 1 + this.m_strokeZmax - this.m_strokeZmin);
            if (num2 < 1)
            {
                return;
            }
            int num3 = 0;
            while (this.GetFreeUndoSpace() < num2 && num3 < 10000)
            {
                this.m_undoList.RemoveAt(0);
                num3++;
            }
            if (num3 >= 10000)
            {
                Debug.Log("InGameTerrainTool:EndStroke: unexpectedly terminated freeing loop, might be a bug.");
                return;
            }
            InGameTerrainTool.UndoStroke item = default(InGameTerrainTool.UndoStroke);
            item.xmin = this.m_strokeXmin;
            item.xmax = this.m_strokeXmax;
            item.zmin = this.m_strokeZmin;
            item.zmax = this.m_strokeZmax;
            item.total_cost = (int)m_totalCost;
            item.pointer = this.m_undoBufferFreePointer;
            this.m_undoList.Add(item);
            ushort[] undoBuffer = Singleton<TerrainManager>.instance.UndoBuffer;
            ushort[] backupHeights = Singleton<TerrainManager>.instance.BackupHeights;
            ushort[] rawHeights = Singleton<TerrainManager>.instance.RawHeights;
            for (int i = this.m_strokeZmin; i <= this.m_strokeZmax; i++)
            {
                for (int j = this.m_strokeXmin; j <= this.m_strokeXmax; j++)
                {
                    int num4 = i * 1081 + j;
                    undoBuffer[this.m_undoBufferFreePointer++] = backupHeights[num4];
                    backupHeights[num4] = rawHeights[num4];
                    this.m_undoBufferFreePointer %= num;
                }
            }
            this.m_strokeXmin = 1080;
            this.m_strokeXmax = 0;
            this.m_strokeZmin = 1080;
            this.m_strokeZmax = 0;
            this.m_totalCost = 0;
        }

        public void ApplyUndo()
        {
            if (this.m_undoList.Count < 1)
            {
                return;
            }
            InGameTerrainTool.UndoStroke undoStroke = this.m_undoList[this.m_undoList.Count - 1];
            this.m_undoList.RemoveAt(this.m_undoList.Count - 1);
            ushort[] undoBuffer = Singleton<TerrainManager>.instance.UndoBuffer;
            ushort[] backupHeights = Singleton<TerrainManager>.instance.BackupHeights;
            ushort[] rawHeights = Singleton<TerrainManager>.instance.RawHeights;
            int num = Singleton<TerrainManager>.instance.UndoBuffer.Length;
            int num2 = Singleton<TerrainManager>.instance.RawHeights.Length;
            int num3 = undoStroke.pointer;
            for (int i = undoStroke.zmin; i <= undoStroke.zmax; i++)
            {
                for (int j = undoStroke.xmin; j <= undoStroke.xmax; j++)
                {
                    int num4 = i * 1081 + j;
                    rawHeights[num4] = undoBuffer[num3];
                    backupHeights[num4] = undoBuffer[num3];
                    num3++;
                    num3 %= num;
                }
            }
            this.m_undoBufferFreePointer = undoStroke.pointer;
            for (int k = 0; k < num2; k++)
            {
                backupHeights[k] = rawHeights[k];
            }
            int num5 = 128;
            undoStroke.xmin = Math.Max(0, undoStroke.xmin - 2);
            undoStroke.xmax = Math.Min(1080, undoStroke.xmax + 2);
            undoStroke.zmin = Math.Max(0, undoStroke.zmin - 2);
            undoStroke.zmax = Math.Min(1080, undoStroke.zmax + 2);
            for (int l = undoStroke.zmin; l <= undoStroke.zmax; l += num5 + 1)
            {
                for (int m = undoStroke.xmin; m <= undoStroke.xmax; m += num5 + 1)
                {
                    TerrainModify.UpdateArea(m, l, m + num5, l + num5, true, false, false);
                }
            }
            this.m_strokeXmin = 1080;
            this.m_strokeXmax = 0;
            this.m_strokeZmin = 1080;
            this.m_strokeZmax = 0;
            if (m_free != true)
            {
                EconomyManager.instance.FetchResource(EconomyManager.Resource.Construction, -undoStroke.total_cost, ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Level.None);
            }

        }

        private void ApplyBrushResource(bool negate)
        {
            float[] brushData = this.m_toolController.BrushData;
            float num = this.m_brushSize * 0.5f;
            float num2 = 33.75f;
            int num3 = 512;
            NaturalResourceManager.ResourceCell[] naturalResources = Singleton<NaturalResourceManager>.instance.m_naturalResources;
            float strength = this.m_strength;
            Vector3 mousePosition = this.m_mousePosition;
            int num4 = Mathf.Max((int)((mousePosition.x - num) / num2 + (float)num3 * 0.5f), 0);
            int num5 = Mathf.Max((int)((mousePosition.z - num) / num2 + (float)num3 * 0.5f), 0);
            int num6 = Mathf.Min((int)((mousePosition.x + num) / num2 + (float)num3 * 0.5f), num3 - 1);
            int num7 = Mathf.Min((int)((mousePosition.z + num) / num2 + (float)num3 * 0.5f), num3 - 1);
            for (int i = num5; i <= num7; i++)
            {
                float num8 = (((float)i - (float)num3 * 0.5f + 0.5f) * num2 - mousePosition.z + num) / this.m_brushSize * 64f - 0.5f;
                int num9 = Mathf.Clamp(Mathf.FloorToInt(num8), 0, 63);
                int num10 = Mathf.Clamp(Mathf.CeilToInt(num8), 0, 63);
                for (int j = num4; j <= num6; j++)
                {
                    float num11 = (((float)j - (float)num3 * 0.5f + 0.5f) * num2 - mousePosition.x + num) / this.m_brushSize * 64f - 0.5f;
                    int num12 = Mathf.Clamp(Mathf.FloorToInt(num11), 0, 63);
                    int num13 = Mathf.Clamp(Mathf.CeilToInt(num11), 0, 63);
                    float num14 = brushData[num9 * 64 + num12];
                    float num15 = brushData[num9 * 64 + num13];
                    float num16 = brushData[num10 * 64 + num12];
                    float num17 = brushData[num10 * 64 + num13];
                    float num18 = num14 + (num15 - num14) * (num11 - (float)num12);
                    float num19 = num16 + (num17 - num16) * (num11 - (float)num12);
                    float num20 = num18 + (num19 - num18) * (num8 - (float)num9);
                    NaturalResourceManager.ResourceCell resourceCell = naturalResources[i * num3 + j];
                    int num21 = (int)(255f * strength * num20);
                    if (negate)
                    {
                        num21 = -num21;
                    }

                    this.ChangeMaterial(ref resourceCell.m_sand, ref resourceCell.m_fertility, num21);

                    naturalResources[i * num3 + j] = resourceCell;
                }
            }
            Singleton<NaturalResourceManager>.instance.AreaModified(num4, num5, num6, num7);
        }

        private void ChangeMaterial(ref byte amount, ref byte other, int change)
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

        ushort targetHeightStroke;

        //deleted parameter input in ApplyPoint()  @SimsFirehouse
        private void ApplyPoint()
        {
            ushort[] rawHeights = TerrainManager.instance.RawHeights;
            Vector3 mousePosition = this.m_mousePosition;

            int minX = Mathf.Max((int)((mousePosition.x) / TerrainManager.RAW_CELL_SIZE + (float)TerrainManager.RAW_RESOLUTION * 0.5f), 0);
            int minZ = Mathf.Max((int)((mousePosition.z) / TerrainManager.RAW_CELL_SIZE + (float)TerrainManager.RAW_RESOLUTION * 0.5f), 0);
            int maxX = Mathf.Min((int)((mousePosition.x + 1) / TerrainManager.RAW_CELL_SIZE + (float)TerrainManager.RAW_RESOLUTION * 0.5f) + 1, TerrainManager.RAW_RESOLUTION);
            int maxZ = Mathf.Min((int)((mousePosition.z + 1) / TerrainManager.RAW_CELL_SIZE + (float)TerrainManager.RAW_RESOLUTION * 0.5f) + 1, TerrainManager.RAW_RESOLUTION);

            ushort maxHeight = 0;
            ushort minHeight = ushort.MaxValue;
            
            for (int i = minZ - 1; i <= maxZ + 1; i++)
            {
                for (int j = minX - 1; j <= maxX + 1; j++)
                {
                    ushort tHeight = rawHeights[i * (TerrainManager.RAW_RESOLUTION + 1) + j];
                    if (tHeight > maxHeight)
                    {
                        maxHeight = tHeight;
                    }
                    if (tHeight < minHeight)
                    {
                        minHeight = tHeight;
                    }
                }
            }

            ushort targetHeight = (ushort)(minHeight);
            if (this.m_mouseRightDown)
                targetHeight = (ushort)(maxHeight);


            var diff = trenchDepth * (this.m_mouseRightDown ? 1 : -1);

            if (!this.m_strokeInProgress)
            {
                targetHeightStroke = (ushort)(targetHeight - diff);
            }
                            
            int m_applyCost = 0;
            bool outOfMoney = false;

            for (int i = minZ; i <= maxZ; i++)
            {
                for (int j = minX; j <= maxX; j++)
                {


                    if (!outOfMoney)
                        m_applyCost += Mathf.Abs(targetHeightStroke - TerrainManager.instance.RawHeights[i * (TerrainManager.RAW_RESOLUTION + 1) + j]) * m_costMultiplier;

                    if ((m_applyCost + m_totalCost < m_lastCash && m_applyCost + m_totalCost < Int32.MaxValue) || m_free == true)
                    {
                        TerrainManager.instance.RawHeights[i * (TerrainManager.RAW_RESOLUTION + 1) + j] = targetHeightStroke;
                        this.m_strokeXmin = Math.Min(this.m_strokeXmin, j);
                        this.m_strokeXmax = Math.Max(this.m_strokeXmax, j);
                        this.m_strokeZmin = Math.Min(this.m_strokeZmin, i);
                        this.m_strokeZmax = Math.Max(this.m_strokeZmax, i);
                    }
                    else
                    {
                        outOfMoney = true;
                    }
                }
            }

            TerrainModify.UpdateArea(minX - 10, minZ - 10, maxX + 10, maxZ + 10, true, false, false);
            if (m_free != true && !outOfMoney)
            {
                EconomyManager.instance.FetchResource(EconomyManager.Resource.Construction, (int)m_applyCost, ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Level.None);
                m_totalCost += m_applyCost;
            }
        }

        private void ApplyBrush()
        {
            long m_applyCost = 0;
            bool outOfMoney = false;
            bool applied = false;
            float[] brushData = this.m_toolController.BrushData;
            float brushRadius = this.m_brushSize * 0.5f;
            float num2 = TerrainManager.RAW_CELL_SIZE;
            int num3 = TerrainManager.RAW_RESOLUTION;
            ushort[] rawHeights = TerrainManager.instance.RawHeights;
            ushort[] finalHeights = TerrainManager.instance.FinalHeights;

            float strength = this.m_strength;
            int smoothStrength = 3;

            float num5 = 0.015625f;
            float num6 = 64f;

            Vector3 mousePosition = this.m_mousePosition;
            Vector3 vector = this.m_endPosition - this.m_startPosition;
            vector.y = 0f;
            float num7 = vector.sqrMagnitude;
            if (num7 != 0f)
            {
                num7 = 1f / num7;
            }
            float num8 = 20f;
            int minX = Mathf.Max((int)((mousePosition.x - brushRadius) / TerrainManager.RAW_CELL_SIZE + (float)TerrainManager.RAW_RESOLUTION * 0.5f), 0);
            int minZ = Mathf.Max((int)((mousePosition.z - brushRadius) / TerrainManager.RAW_CELL_SIZE + (float)TerrainManager.RAW_RESOLUTION * 0.5f), 0);
            int maxX = Mathf.Min((int)((mousePosition.x + brushRadius) / TerrainManager.RAW_CELL_SIZE + (float)TerrainManager.RAW_RESOLUTION * 0.5f) + 1, TerrainManager.RAW_RESOLUTION);
            int maxZ = Mathf.Min((int)((mousePosition.z + brushRadius) / TerrainManager.RAW_CELL_SIZE + (float)TerrainManager.RAW_RESOLUTION * 0.5f) + 1, TerrainManager.RAW_RESOLUTION);
            if (this.m_mode == InGameTerrainTool.Mode.Shift)
            {
                if (this.m_mouseRightDown)
                {
                    num8 = -num8;
                }
            }
            else if (this.m_mode == InGameTerrainTool.Mode.Soften && this.m_mouseRightDown)
            {
                smoothStrength = 10;
            }
            for (int i = minZ; i <= maxZ; i++)
            {
                float num13 = (((float)i - (float)num3 * 0.5f) * num2 - mousePosition.z + brushRadius) / this.m_brushSize * 64f - 0.5f;
                int num14 = Mathf.Clamp(Mathf.FloorToInt(num13), 0, 63);
                int num15 = Mathf.Clamp(Mathf.CeilToInt(num13), 0, 63);
                for (int j = minX; j <= maxX; j++)
                {
                    float num16 = (((float)j - (float)num3 * 0.5f) * num2 - mousePosition.x + brushRadius) / this.m_brushSize * 64f - 0.5f;
                    int num17 = Mathf.Clamp(Mathf.FloorToInt(num16), 0, 63);
                    int num18 = Mathf.Clamp(Mathf.CeilToInt(num16), 0, 63);
                    float num19 = brushData[num14 * 64 + num17];
                    float num20 = brushData[num14 * 64 + num18];
                    float num21 = brushData[num15 * 64 + num17];
                    float num22 = brushData[num15 * 64 + num18];
                    float num23 = num19 + (num20 - num19) * (num16 - (float)num17);
                    float num24 = num21 + (num22 - num21) * (num16 - (float)num17);
                    float num25 = num23 + (num24 - num23) * (num13 - (float)num14);
                    float num26 = (float)rawHeights[i * (num3 + 1) + j] * num5;
                    float num27 = 0f;
                    if (this.m_mode == InGameTerrainTool.Mode.Shift)
                    {
                        num27 = num26 + num8;
                    }
                    else if (this.m_mode == InGameTerrainTool.Mode.Level)
                    {
                        num27 = this.m_startPosition.y;
                    }
                    else if (this.m_mode == InGameTerrainTool.Mode.Soften)
                    {
                        int num28 = Mathf.Max(j - smoothStrength, 0);
                        int num29 = Mathf.Max(i - smoothStrength, 0);
                        int num30 = Mathf.Min(j + smoothStrength, num3);
                        int num31 = Mathf.Min(i + smoothStrength, num3);
                        float num32 = 0f;
                        for (int k = num29; k <= num31; k++)
                        {
                            for (int l = num28; l <= num30; l++)
                            {
                                float num33 = 1f - (float)((l - j) * (l - j) + (k - i) * (k - i)) / (float)(smoothStrength * smoothStrength);
                                if (num33 > 0f)
                                {
                                    num27 += (float)finalHeights[k * (num3 + 1) + l] * (num5 * num33);
                                    num32 += num33;
                                }
                            }
                        }
                        num27 /= num32;
                    }
                    else if (this.m_mode == InGameTerrainTool.Mode.Slope)
                    {
                        float num34 = ((float)j - (float)num3 * 0.5f) * num2;
                        float num35 = ((float)i - (float)num3 * 0.5f) * num2;
                        float t = ((num34 - this.m_startPosition.x) * vector.x + (num35 - this.m_startPosition.z) * vector.z) * num7;
                        num27 = Mathf.Lerp(this.m_startPosition.y, this.m_endPosition.y, t);
                    }
                    num27 = Mathf.Lerp(num26, num27, strength * num25);

                    var oldHeight = rawHeights[i * (num3 + 1) + j];
                    var newHeight = (ushort)Mathf.Clamp(Mathf.RoundToInt(num27 * num6), 0, 65535);

                    if (!outOfMoney)
                        m_applyCost += Mathf.Abs(newHeight - oldHeight) * m_costMultiplier;

                    if ((m_applyCost + m_totalCost < m_lastCash && m_applyCost + m_totalCost < Int32.MaxValue) || m_free == true)
                    {
                        rawHeights[i * (num3 + 1) + j] = newHeight;
                        this.m_strokeXmin = Math.Min(this.m_strokeXmin, j);
                        this.m_strokeXmax = Math.Max(this.m_strokeXmax, j);
                        this.m_strokeZmin = Math.Min(this.m_strokeZmin, i);
                        this.m_strokeZmax = Math.Max(this.m_strokeZmax, i);
                        applied = true;
                    }
                    else
                    {
                        outOfMoney = true;
                    }

                }
            }
            TerrainModify.UpdateArea(minX, minZ, maxX, maxZ, true, true, false);

            if (applied)
            {
                if (m_free != true)
                {
                    EconomyManager.instance.FetchResource(EconomyManager.Resource.Construction, (int)m_applyCost, ItemClass.Service.None, ItemClass.SubService.None, ItemClass.Level.None);
                    m_totalCost += m_applyCost;
                }
            }
        }

    }



}