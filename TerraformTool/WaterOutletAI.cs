using ColossalFramework;
using ColossalFramework.DataBinding;
using ColossalFramework.Math;
using System;
using UnityEngine;
namespace TerraformTool
{
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

        public Vector3 m_waterLocationOffset = new Vector3(0, 0, 1280);
        [CustomizableProperty("MaxWaterDistance", "Water")]
        public float m_maxWaterDistance = 100f;

        [CustomizableProperty("NoiseAccumulation", "Pollution")]
        public int m_noiseAccumulation = 50;
        [CustomizableProperty("NoiseRadius", "Pollution")]
        public float m_noiseRadius = 100f;

        int m_consumption_multiplier = 750;
        int m_bufferMultiplier = 10;

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
                //if (this.m_sewageOutlet != 0)
                //{
                //    return Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColorB;
                //}
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
                //if (this.m_sewageOutlet != 0)
                //{
                //    return Singleton<InfoManager>.instance.m_properties.m_modeProperties[(int)infoMode].m_activeColor;
                //}
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
            return productionRate * (this.m_waterConsumption) / 100;
        }

        public override void GetPlacementInfoMode(out InfoManager.InfoMode mode, out InfoManager.SubInfoMode subMode)
        {
            mode = InfoManager.InfoMode.None;
            subMode = InfoManager.SubInfoMode.FlowingWater;
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
                NetManager instance = NetManager.instance;
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

            int waterProdMp = m_waterConsumption * m_consumption_multiplier;

            

            int waterRate = waterProdMp;

            //Debug.Log("Produce goods");
            //Debug.Log("waterRate      = " + waterRate);
            //Debug.Log("productionRate = " + productionRate);

            if (waterRate != 0)
            {
                int bufferedWater = (int)(buildingData.m_waterBuffer * m_bufferMultiplier);
                
                //Debug.Log("bufferedWater = " + bufferedWater);

                int missingWater = waterRate * 10 - bufferedWater;

                //Debug.Log("missingWater = " + missingWater);

                if (missingWater > 0)
                {
                    byte pollution = 0;
                    int fetchedWater = WaterManager.instance.TryFetchWater(buildingData.m_position, missingWater, missingWater, ref pollution);

                    if(buildingData.m_productionRate < 100)
                    {
                        int fetchedWater2 = WaterManager.instance.TryFetchWater(buildingData.m_position, missingWater / 2, missingWater / 2, ref pollution);

                        if (fetchedWater2 > 0)
                        {
                            if (buildingData.m_productionRate + 3 <= 100)
                                buildingData.m_productionRate += 3;
                        }
                    }
                    

                    //Debug.Log("fetchedWater = " + fetchedWater);

                    bufferedWater += fetchedWater;
                }

                

                int waterFinalRate = waterRate * productionRate / 100;

                if (bufferedWater - waterFinalRate > 0)
                {
                    //Debug.Log("dumping water");
                    bufferedWater -= waterFinalRate;

                    // Dump water to env
                    this.HandleWaterSource(buildingID, ref buildingData, true, waterFinalRate, waterFinalRate, this.m_maxWaterDistance);

                }

                //if (bufferedWater - waterFinalRate * 4 > 0)
                //{
                //    if (buildingData.m_productionRate + 3 <= 100)
                //        buildingData.m_productionRate += 3;
                //}

                if (bufferedWater - waterFinalRate * 4 < 0)
                {
                    if (buildingData.m_productionRate > 3)
                        buildingData.m_productionRate -= 3;
                }

                // Decrease buffer
                buildingData.m_waterBuffer = (ushort)((bufferedWater) / m_bufferMultiplier);


                //Debug.Log("m_waterBuffer =" + bufferedWater);

                DistrictManager.instance.m_districts.m_buffer[district].m_playerConsumption.m_tempWaterConsumption += (uint)waterRate;
            }


            base.HandleDead(buildingID, ref buildingData, ref behaviour, totalWorkerCount);
            int noiseRate = productionRate * this.m_noiseAccumulation / 100;
            if (noiseRate != 0)
            {
                Singleton<ImmaterialResourceManager>.instance.AddResource(ImmaterialResourceManager.Resource.NoisePollution, noiseRate, buildingData.m_position, this.m_noiseRadius);
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
                    //  sourceData2.m_pollution = num * (uint)this.m_outletPollution / (uint)Mathf.Max(100, waterSimulation.GetPollutionDisposeRate() * 100);
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

        public override void PlacementSucceeded()
        {
            if (this.m_waterConsumption != 0)
            {
                BuildingTypeGuide drainPipeMissingGuide = Singleton<WaterManager>.instance.m_drainPipeMissingGuide;
                if (drainPipeMissingGuide != null)
                {
                    drainPipeMissingGuide.Deactivate();
                }
            }
            //if (this.m_sewageOutlet != 0)
            //{
            //    BuildingTypeGuide drainPipeMissingGuide = Singleton<WaterManager>.instance.m_drainPipeMissingGuide;
            //    if (drainPipeMissingGuide != null)
            //    {
            //        drainPipeMissingGuide.Deactivate();
            //    }
            //}
        }

        public override void UpdateGuide(GuideController guideController)
        {
            if (this.m_waterConsumption != 0)
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
            if (this.m_waterConsumption > 0)
            {
                return TooltipHelper.Append(base.GetLocalizedTooltip(), TooltipHelper.Format(new string[]
			{
				LocaleFormatter.Info1,
				text,
				LocaleFormatter.Info2,
				LocaleFormatter.FormatGeneric("AIINFO_WATER_INTAKE", new object[]
				{
					this.m_waterConsumption * 16 * m_consumption_multiplier
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
				this.m_waterConsumption * 16  * m_consumption_multiplier
			})
		}));
        }

        public override string GetLocalizedStats(ushort buildingID, ref Building data)
        {
            string text = string.Empty;
            if (this.m_waterConsumption > 0)
            {
                int num = this.GetWaterRate(buildingID, ref data) * 16 * m_consumption_multiplier;
                text += LocaleFormatter.FormatGeneric("AIINFO_WATER_INTAKE", new object[]
			{
				num
			});
            }
            else
            {
                int num2 = -this.GetWaterRate(buildingID, ref data) * 16 * m_consumption_multiplier;
                text += LocaleFormatter.FormatGeneric("AIINFO_WATER_OUTLET", new object[] { num2 });
            }
            text += "\nStored water: " + (data.m_waterBuffer * m_bufferMultiplier) + " m³";
            text += "\nRunning at: " + (data.m_productionRate) + "%";
            return text;
        }
    }

}