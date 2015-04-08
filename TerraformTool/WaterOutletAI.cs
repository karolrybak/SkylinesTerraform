using ColossalFramework;
using ColossalFramework.DataBinding;
using ColossalFramework.Math;
using System;
using UnityEngine;
public class WaterOutletAI : PlayerBuildingAI
{
    [CustomizableProperty("UneducatedWorkers", "Workers")]
    public int m_workPlaceCount0 = 10;
    [CustomizableProperty("EducatedWorkers", "Workers")]
    public int m_workPlaceCount1 = 10;
    [CustomizableProperty("WellEducatedWorkers", "Workers")]
    public int m_workPlaceCount2 = 10;
    [CustomizableProperty("HighlyEducatedWorkers", "Workers")]
    public int m_workPlaceCount3 = 10;
    [CustomizableProperty("WaterIntake", "Water")]
    public int m_waterIntake = 1000;
    [CustomizableProperty("SewageOutlet", "Water")]
    public int m_sewageOutlet = 0;
    public Vector3 m_waterLocationOffset = new Vector3(0, 0, 32);
    [CustomizableProperty("MaxWaterDistance", "Water")]
    public float m_maxWaterDistance = 100f;
    [CustomizableProperty("UseGroundWater", "Water")]
    public bool m_useGroundWater;
    [CustomizableProperty("OutletPollution", "Pollution")]
    public int m_outletPollution = 0;
    [CustomizableProperty("NoiseAccumulation", "Pollution")]
    public int m_noiseAccumulation = 50;
    [CustomizableProperty("NoiseRadius", "Pollution")]
    public float m_noiseRadius = 100f;
    public override void GetNaturalResourceRadius(ushort buildingID, ref Building data, out NaturalResourceManager.Resource resource1, out Vector3 position1, out float radius1, out NaturalResourceManager.Resource resource2, out Vector3 position2, out float radius2)
    {
        resource1 = NaturalResourceManager.Resource.Water;
        position1 = data.CalculatePosition(this.m_waterLocationOffset);
        radius1 = this.m_maxWaterDistance;
        resource2 = NaturalResourceManager.Resource.None;
        position2 = data.m_position;
        radius2 = 0f;
    }
    public override void GetImmaterialResourceRadius(ushort buildingID, ref Building data, out ImmaterialResourceManager.Resource resource1, out float radius1, out ImmaterialResourceManager.Resource resource2, out float radius2)
    {
        if (this.m_noiseAccumulation != 0)
        {
            resource1 = ImmaterialResourceManager.Resource.NoisePollution;
            radius1 = this.m_noiseRadius;
        }
        else
        {
            resource1 = ImmaterialResourceManager.Resource.None;
            radius1 = 0f;
        }
        resource2 = ImmaterialResourceManager.Resource.None;
        radius2 = 0f;
    }
    public override Color GetColor(ushort buildingID, ref Building data, InfoManager.InfoMode infoMode)
    {
        Debug.Log("GetColor");
        Debug.Log(buildingID);
        Debug.Log(data);
        Debug.Log(infoMode);

        if (infoMode == InfoManager.InfoMode.Water)
        {
            if ((data.m_flags & Building.Flags.Active) == Building.Flags.None)
            {
                return Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_inactiveColor;
            }
            if (this.m_waterIntake != 0)
            {
                return Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColor;
            }
            if (this.m_sewageOutlet != 0)
            {
                return Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColorB;
            }
            return base.GetColor(buildingID, ref data, infoMode);
        }
        else
        {
            if (infoMode == InfoManager.InfoMode.NoisePollution)
            {
                int noiseAccumulation = this.m_noiseAccumulation;
                return CommonBuildingAI.GetNoisePollutionColor((float)noiseAccumulation);
            }
            if (infoMode != InfoManager.InfoMode.Pollution)
            {
                return base.GetColor(buildingID, ref data, infoMode);
            }
            if (this.m_waterIntake != 0)
            {
                float t = Mathf.Clamp01((float)data.m_waterPollution * 0.0117647061f);
                return ColorUtils.LinearLerp(Singleton<InfoManager>.instance.m_properties.m_neutralColor, Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColor, t);
            }
            if (this.m_sewageOutlet != 0)
            {
                return Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColor;
            }
            return base.GetColor(buildingID, ref data, infoMode);
        }
    }
    public override int GetResourceRate(ushort buildingID, ref Building data, ImmaterialResourceManager.Resource resource)
    {
        if (resource == ImmaterialResourceManager.Resource.NoisePollution)
        {
            return this.m_noiseAccumulation;
        }
        return base.GetResourceRate(buildingID, ref data, resource);
    }
    public override int GetWaterRate(ushort buildingID, ref Building data)
    {
        int productionRate = (int)data.m_productionRate;
        int budget = Singleton<EconomyManager>.instance.GetBudget(this.m_info.m_class);
        productionRate = PlayerBuildingAI.GetProductionRate(productionRate, budget);
        return productionRate * (this.m_waterIntake - this.m_sewageOutlet) / 100;
    }

    public override void GetPlacementInfoMode(out InfoManager.InfoMode mode, out InfoManager.SubInfoMode subMode)
    {
        mode = InfoManager.InfoMode.Water;
        if (this.m_useGroundWater)
        {
            subMode = InfoManager.SubInfoMode.WindPower;
        }
        else
        {
            subMode = InfoManager.SubInfoMode.WaterPower;
        }
    }
    public override void CreateBuilding(ushort buildingID, ref Building data)
    {
        base.CreateBuilding(buildingID, ref data);
        int workCount = this.m_workPlaceCount0 + this.m_workPlaceCount1 + this.m_workPlaceCount2 + this.m_workPlaceCount3;
        Singleton<CitizenManager>.instance.CreateUnits(out data.m_citizenUnits, ref Singleton<SimulationManager>.instance.m_randomizer, buildingID, 0, 0, workCount, 0, 0, 0);
    }
    public override void ReleaseBuilding(ushort buildingID, ref Building data)
    {
        base.ReleaseBuilding(buildingID, ref data);
    }
    protected override void ManualActivation(ushort buildingID, ref Building buildingData)
    {
        Vector3 position = buildingData.m_position;
        position.y += this.m_info.m_size.y;
        Singleton<NotificationManager>.instance.AddEvent(NotificationEvent.Type.GainWater, position, 1.5f);
        if (this.m_noiseAccumulation != 0)
        {
            Singleton<NotificationManager>.instance.AddWaveEvent(buildingData.m_position, NotificationEvent.Type.Sad, ImmaterialResourceManager.Resource.NoisePollution, (float)this.m_noiseAccumulation, this.m_noiseRadius);
        }
    }
    protected override void ManualDeactivation(ushort buildingID, ref Building buildingData)
    {
        Vector3 position = buildingData.m_position;
        position.y += this.m_info.m_size.y;
        Singleton<NotificationManager>.instance.AddEvent(NotificationEvent.Type.LoseWater, position, 1.5f);
        if (this.m_noiseAccumulation != 0)
        {
            Singleton<NotificationManager>.instance.AddWaveEvent(buildingData.m_position, NotificationEvent.Type.Happy, ImmaterialResourceManager.Resource.NoisePollution, (float)(-(float)this.m_noiseAccumulation), this.m_noiseRadius);
        }
    }
    public override int GetConstructionCost()
    {
        return 1500;
    }

    public override bool CheckUnlocking()
    {
        return true;
    }

    public override ToolBase.ToolErrors CheckBuildPosition(ushort relocateID, ref Vector3 position, ref float angle, float waterHeight, ref Segment3 connectionSegment, out int productionRate, out int constructionCost)
    {
        ToolBase.ToolErrors toolErrors = base.CheckBuildPosition(relocateID, ref position, ref angle, waterHeight, ref connectionSegment, out productionRate, out constructionCost);
        return toolErrors;
    }
    protected override void HandleWorkAndVisitPlaces(ushort buildingID, ref Building buildingData, ref Citizen.BehaviourData behaviour, ref int aliveWorkerCount, ref int totalWorkerCount, ref int workPlaceCount, ref int aliveVisitorCount, ref int totalVisitorCount, ref int visitPlaceCount)
    {
        workPlaceCount += this.m_workPlaceCount0 + this.m_workPlaceCount1 + this.m_workPlaceCount2 + this.m_workPlaceCount3;
        base.GetWorkBehaviour(buildingID, ref buildingData, ref behaviour, ref aliveWorkerCount, ref totalWorkerCount);
        base.HandleWorkPlaces(buildingID, ref buildingData, this.m_workPlaceCount0, this.m_workPlaceCount1, this.m_workPlaceCount2, this.m_workPlaceCount3, ref behaviour, aliveWorkerCount, totalWorkerCount);
    }
    protected override void ProduceGoods(ushort buildingID, ref Building buildingData, ref Building.Frame frameData, int productionRate, ref Citizen.BehaviourData behaviour, int aliveWorkerCount, int totalWorkerCount, int workPlaceCount, int aliveVisitorCount, int totalVisitorCount, int visitPlaceCount)
    {
        bool flag = false;
        if (buildingData.m_netNode != 0)
        {
            NetManager instance = Singleton<NetManager>.instance;
            for (int i = 0; i < 8; i++)
            {
                if (instance.m_nodes.m_buffer[(int)buildingData.m_netNode].GetSegment(i) != 0)
                {
                    flag = true;
                    break;
                }
            }
        }
        if (!flag)
        {
            productionRate = 0;
        }
        DistrictManager instance2 = Singleton<DistrictManager>.instance;
        byte district = instance2.GetDistrict(buildingData.m_position);
        int num = this.m_waterIntake * productionRate / 100;
        if (num != 0)
        {
            int num2 = (int)(buildingData.m_customBuffer1 * 1000 + buildingData.m_waterBuffer);
            int num3 = num * 2 - num2;
            if (num3 > 0)
            {
                int num4;
                if (this.m_useGroundWater)
                {
                    num4 = num3;
                    Vector3 pos = buildingData.CalculatePosition(this.m_waterLocationOffset);
                    Singleton<NaturalResourceManager>.instance.CheckPollution(pos, out buildingData.m_waterPollution);
                }
                else
                {
                    num4 = this.HandleWaterSource(buildingID, ref buildingData, false, num, num3, this.m_maxWaterDistance);
                }
                num2 += num4;
                if (num4 == 0)
                {
                    productionRate = 0;
                }
            }
            else
            {
                productionRate = 0;
            }
            int num5 = Mathf.Min(num2, num);
            if (num5 > 0)
            {
                int num6 = Singleton<WaterManager>.instance.TryDumpWater(buildingData.m_netNode, num, num5, buildingData.m_waterPollution);
                num2 -= num6;
            }
            if (this.m_useGroundWater || buildingData.m_waterSource != 0)
            {
                District[] expr_16A_cp_0_cp_0 = instance2.m_districts.m_buffer;
                byte expr_16A_cp_0_cp_1 = district;
                expr_16A_cp_0_cp_0[(int)expr_16A_cp_0_cp_1].m_productionData.m_tempWaterCapacity = expr_16A_cp_0_cp_0[(int)expr_16A_cp_0_cp_1].m_productionData.m_tempWaterCapacity + (uint)num;
            }
            buildingData.m_customBuffer1 = (ushort)(num2 / 1000);
            buildingData.m_waterBuffer = (ushort)(num2 - (int)(buildingData.m_customBuffer1 * 1000));
        }
        int num7 = this.m_sewageOutlet * productionRate / 100;
        if (num7 != 0)
        {
            int num8 = (int)(buildingData.m_customBuffer2 * 1000 + buildingData.m_sewageBuffer);
            int num9 = num7 * 4 - num8;
            if (num9 > 0)
            {
                byte pollution = 0;
                int num10 = Singleton<WaterManager>.instance.TryFetchWater(buildingData.m_position, num7, num9, ref pollution);
                num8 += num10;
            }
            int num11 = Mathf.Min(num8 - num7 * 2, num7);
            if (num11 > 0)
            {
                int num12;
                if (this.m_useGroundWater)
                {
                    num12 = num11;
                    Vector3 position = buildingData.CalculatePosition(this.m_waterLocationOffset);
                    int num13 = (num11 * this.m_outletPollution + 99) / 100;
                    Singleton<NaturalResourceManager>.instance.TryDumpResource(NaturalResourceManager.Resource.Water, num13, num13, position, this.m_maxWaterDistance);
                }
                else
                {
                    num12 = this.HandleWaterSource(buildingID, ref buildingData, true, num7, num11, this.m_maxWaterDistance);
                }
                num8 -= num12;
                if (num12 == 0)
                {
                    productionRate = 0;
                }
            }
            else
            {
                productionRate = 0;
            }
            if (this.m_useGroundWater || buildingData.m_waterSource != 0)
            {
                District[] expr_2B4_cp_0_cp_0 = instance2.m_districts.m_buffer;
                byte expr_2B4_cp_0_cp_1 = district;
                expr_2B4_cp_0_cp_0[(int)expr_2B4_cp_0_cp_1].m_productionData.m_tempSewageCapacity = expr_2B4_cp_0_cp_0[(int)expr_2B4_cp_0_cp_1].m_productionData.m_tempSewageCapacity + (uint)num7;
            }
            buildingData.m_customBuffer2 = (ushort)(num8 / 1000);
            buildingData.m_sewageBuffer = (ushort)(num8 - (int)(buildingData.m_customBuffer2 * 1000));
        }
        base.HandleDead(buildingID, ref buildingData, ref behaviour, totalWorkerCount);
        int num14 = productionRate * this.m_noiseAccumulation / 100;
        if (num14 != 0)
        {
            Singleton<ImmaterialResourceManager>.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, num14, buildingData.m_position, this.m_noiseRadius);
        }
        base.ProduceGoods(buildingID, ref buildingData, ref frameData, productionRate, ref behaviour, aliveWorkerCount, totalWorkerCount, workPlaceCount, aliveVisitorCount, totalVisitorCount, visitPlaceCount);
    }
    protected override bool CanSufferFromFlood()
    {
        return false;
    }
    private int HandleWaterSource(ushort buildingID, ref Building data, bool output, int rate, int max, float radius)
    {
        uint num = (uint)(Mathf.Min(rate, max) >> 1);
        if (num == 0u)
        {
            return 0;
        }
        TerrainManager instance = Singleton<TerrainManager>.instance;
        WaterSimulation waterSimulation = instance.WaterSimulation;
        if (data.m_waterSource != 0)
        {
            bool flag = false;
            WaterSource sourceData = waterSimulation.LockWaterSource(data.m_waterSource);
            try
            {
                if (output)
                {
                    uint num2 = num;
                    if (num2 < sourceData.m_water >> 3)
                    {
                        num2 = sourceData.m_water >> 3;
                    }
                    sourceData.m_outputRate = num2 + 3u >> 2;
                    sourceData.m_water += num;
                    sourceData.m_pollution += num * (uint)this.m_outletPollution / (uint)Mathf.Max(100, waterSimulation.GetPollutionDisposeRate() * 100);
                }
                else
                {
                    uint num3 = num;
                    if (num3 < sourceData.m_water >> 3)
                    {
                        num3 >>= 1;
                    }
                    sourceData.m_inputRate = num3 + 3u >> 2;
                    if (num > sourceData.m_water)
                    {
                        num = sourceData.m_water;
                    }
                    if (sourceData.m_water != 0u)
                    {
                        data.m_waterPollution = (byte)Mathf.Min(255f, 255u * sourceData.m_pollution / sourceData.m_water);
                        sourceData.m_pollution = (uint)((ulong)sourceData.m_pollution * (ulong)(sourceData.m_water - num) / (ulong)sourceData.m_water);
                    }
                    else
                    {
                        data.m_waterPollution = 0;
                    }
                    sourceData.m_water -= num;
                    Vector3 vector = sourceData.m_inputPosition;
                    if (!instance.HasWater(VectorUtils.XZ(vector)))
                    {
                        vector = data.CalculatePosition(this.m_waterLocationOffset);
                        if (instance.GetClosestWaterPos(ref vector, radius))
                        {
                            sourceData.m_inputPosition = vector;
                            sourceData.m_outputPosition = vector;
                        }
                        else
                        {
                            flag = true;
                        }
                    }
                }
            }
            finally
            {
                waterSimulation.UnlockWaterSource(data.m_waterSource, sourceData);
            }
            if (flag)
            {
                waterSimulation.ReleaseWaterSource(data.m_waterSource);
                data.m_waterSource = 0;
            }
        }
        else
        {
            Vector3 vector2 = data.CalculatePosition(this.m_waterLocationOffset);
            WaterSource sourceData2 = default(WaterSource);
            sourceData2.m_type = 2;
            sourceData2.m_inputPosition = vector2;
            sourceData2.m_outputPosition = vector2;
            
            if (output)
            {
                sourceData2.m_outputRate = num + 3u >> 2;
                sourceData2.m_water = num;
                sourceData2.m_pollution = num * (uint)this.m_outletPollution / (uint)Mathf.Max(100, waterSimulation.GetPollutionDisposeRate() * 100);
                if (!waterSimulation.CreateWaterSource(out data.m_waterSource, sourceData2))
                {
                    num = 0u;
                }
            }
            else
            {
                sourceData2.m_inputRate = num + 3u >> 2;
                waterSimulation.CreateWaterSource(out data.m_waterSource, sourceData2);
                num = 0u;
            }

        }
        return (int)((int)num << 1);
    }
    public override void PlacementFailed()
    {
        if (!this.m_useGroundWater)
        {
            GuideController properties = Singleton<GuideManager>.instance.m_properties;
            if (properties != null)
            {
                Singleton<BuildingManager>.instance.m_buildNextToWater.Activate(properties.m_buildNextToWater);
            }
        }
    }
    public override void PlacementSucceeded()
    {
        if (!this.m_useGroundWater)
        {
            GenericGuide buildNextToWater = Singleton<BuildingManager>.instance.m_buildNextToWater;
            if (buildNextToWater != null)
            {
                buildNextToWater.Deactivate();
            }
        }
        if (this.m_waterIntake != 0)
        {
            BuildingTypeGuide waterPumpMissingGuide = Singleton<WaterManager>.instance.m_waterPumpMissingGuide;
            if (waterPumpMissingGuide != null)
            {
                waterPumpMissingGuide.Deactivate();
            }
        }
        if (this.m_sewageOutlet != 0)
        {
            BuildingTypeGuide drainPipeMissingGuide = Singleton<WaterManager>.instance.m_drainPipeMissingGuide;
            if (drainPipeMissingGuide != null)
            {
                drainPipeMissingGuide.Deactivate();
            }
        }
    }
    public override void UpdateGuide(GuideController guideController)
    {
        if (this.m_waterIntake != 0)
        {
            BuildingTypeGuide waterPumpMissingGuide = Singleton<WaterManager>.instance.m_waterPumpMissingGuide;
            if (waterPumpMissingGuide != null)
            {
                int waterCapacity = Singleton<DistrictManager>.instance.m_districts.m_buffer[0].GetWaterCapacity();
                int sewageCapacity = Singleton<DistrictManager>.instance.m_districts.m_buffer[0].GetSewageCapacity();
                if (waterCapacity == 0 && sewageCapacity != 0)
                {
                    waterPumpMissingGuide.Activate(guideController.m_waterPumpMissing, this.m_info);
                }
                else
                {
                    waterPumpMissingGuide.Deactivate();
                }
            }
        }
        if (this.m_sewageOutlet != 0)
        {
            BuildingTypeGuide drainPipeMissingGuide = Singleton<WaterManager>.instance.m_drainPipeMissingGuide;
            if (drainPipeMissingGuide != null)
            {
                int waterCapacity2 = Singleton<DistrictManager>.instance.m_districts.m_buffer[0].GetWaterCapacity();
                int sewageCapacity2 = Singleton<DistrictManager>.instance.m_districts.m_buffer[0].GetSewageCapacity();
                if (waterCapacity2 != 0 && sewageCapacity2 == 0)
                {
                    drainPipeMissingGuide.Activate(guideController.m_drainPipeMissing, this.m_info);
                }
                else
                {
                    drainPipeMissingGuide.Deactivate();
                }
            }
        }
        base.UpdateGuide(guideController);
    }
    public override void GetPollutionAccumulation(out int ground, out int noise)
    {
        ground = 0;
        noise = this.m_noiseAccumulation;
    }
    public override string GetLocalizedTooltip()
    {
        string text = LocaleFormatter.FormatGeneric("AIINFO_WATER_CONSUMPTION", new object[]
		{
			this.GetWaterConsumption() * 16
		}) + Environment.NewLine + LocaleFormatter.FormatGeneric("AIINFO_ELECTRICITY_CONSUMPTION", new object[]
		{
			this.GetElectricityConsumption() * 16
		});
        if (this.m_waterIntake > 0)
        {
            return TooltipHelper.Append(base.GetLocalizedTooltip(), TooltipHelper.Format(new string[]
			{
				LocaleFormatter.Info1,
				text,
				LocaleFormatter.Info2,
				LocaleFormatter.FormatGeneric("AIINFO_WATER_INTAKE", new object[]
				{
					this.m_waterIntake * 16
				})
			}));
        }
        return TooltipHelper.Append(base.GetLocalizedTooltip(), TooltipHelper.Format(new string[]
		{
			LocaleFormatter.Info1,
			text,
			LocaleFormatter.Info2,
			LocaleFormatter.FormatGeneric("AIINFO_WATER_OUTLET", new object[]
			{
				this.m_sewageOutlet * 16
			})
		}));
    }
    public override string GetLocalizedStats(ushort buildingID, ref Building data)
    {
        string text = string.Empty;
        if (this.m_waterIntake > 0)
        {
            int num = this.GetWaterRate(buildingID, ref data) * 16;
            text += LocaleFormatter.FormatGeneric("AIINFO_WATER_INTAKE", new object[]
			{
				num
			});
        }
        else
        {
            int num2 = -this.GetWaterRate(buildingID, ref data) * 16;
            text += LocaleFormatter.FormatGeneric("AIINFO_WATER_OUTLET", new object[]
			{
				num2
			});
        }
        return text;
    }
}
