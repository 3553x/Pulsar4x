﻿using System;
using System.Collections.Generic;
using System.Linq;

namespace Pulsar4X.ECSLib
{
    public static class InstallationProcessor
    {
        /// <summary>
        /// this should be the main entry point for doing stuff.
        /// </summary>
        /// <param name="colonyEntity"></param>
        /// <param name="factionEntity"></param>
        public static void PerEconTic(Entity colonyEntity, Entity factionEntity)
        {
            
            Employment(colonyEntity); //check if installations still work
            Mine(factionEntity, colonyEntity); //mine new materials.
            Construction(factionEntity, colonyEntity); //construct, refine, etc. 
            
        }

        /// <summary>
        /// should be called when new facilitys are added, 
        /// facilies are enabled or disabled, 
        /// or if population changes significantly.
        /// Or maybe just check at the beginning of every econ tick.
        /// </summary>
        /// <param name="colonyEntity"></param>
        public static void Employment(Entity colonyEntity)
        {
            var employablePopulationlist = colonyEntity.GetDataBlob<ColonyInfoDB>().Population.Values;
            long employable = (long)(employablePopulationlist.Sum() * 1000000); //because it's in millions I think...maybe we should change.
            InstallationsDB installationsDB = colonyEntity.GetDataBlob<InstallationsDB>();
            //int totalReq = 0;
            JDictionary<Guid,int> workingInstallations  = new JDictionary<Guid, int>(StaticDataManager.StaticDataStore.Installations.Keys.ToDictionary(key => key, val => 0));
            foreach (var type in installationsDB.EmploymentList)
            {
                //totalReq += type.Key.PopulationRequired * (int)type.Value;
                var fac = StaticDataManager.StaticDataStore.Installations[type.Type];
                if (type.Enabled && employable >= fac.PopulationRequired)
                {
                    employable -= fac.PopulationRequired;
                    workingInstallations[type.Type] += 1;
                }
            }
            installationsDB.WorkingInstallations = workingInstallations;
        }

        /// <summary>
        /// run every econ tic 
        /// extracts minerals from planet surface by mineing ability;
        /// </summary>
        /// <param name="factionEntity"></param>
        public static void Mine(Entity factionEntity, Entity colonyEntity)
        {
            float factionMineingAbility = factionEntity.GetDataBlob<FactionAbilitiesDB>().BaseMiningBonus;
            float sectorGovenerAbility = 1.0f; //todo these guys dont exsist yet
            float planetGovenerAbility = 1.0f; //todo these guys dont exsist yet
            float totalBonusMultiplier = factionMineingAbility * sectorGovenerAbility * planetGovenerAbility;

            Entity planetEntity = colonyEntity.GetDataBlob<ColonyInfoDB>().PlanetEntity;
            SystemBodyDB planetDB = planetEntity.GetDataBlob<SystemBodyDB>();
            JDictionary<Guid, int> colonyMineralStockpile = colonyEntity.GetDataBlob<ColonyInfoDB>().MineralStockpile;
            int installationMineingAbility = InstallationAbilityofType(InstallationAbilityType.Mine, colonyEntity.GetDataBlob<InstallationsDB>());
            JDictionary<Guid, MineralDepositInfo> planetRawMinerals = planetDB.Minerals;

            foreach (KeyValuePair<Guid, MineralDepositInfo> depositKeyValuePair in planetRawMinerals)
            {
                Guid mineralGuid = depositKeyValuePair.Key;
                int amountOnPlanet = depositKeyValuePair.Value.Amount;
                double accessibility = depositKeyValuePair.Value.Accessibility;
                double abilitiestoMine = installationMineingAbility * totalBonusMultiplier * accessibility;
                int amounttomine = (int)Math.Min(abilitiestoMine, amountOnPlanet);
                colonyMineralStockpile[mineralGuid] += amounttomine;
                MineralDepositInfo mineralDeposit = depositKeyValuePair.Value;
                mineralDeposit.Amount -= amounttomine;
                double accecability = Math.Pow((float)mineralDeposit.Amount / mineralDeposit.HalfOrigionalAmount, 3) * mineralDeposit.Accessibility;
                mineralDeposit.Accessibility = GMath.Clamp(accecability, 0.1, mineralDeposit.Accessibility);
            }
            
        }

        /// <summary>
        /// runs each of the constructionJob lists.
        /// </summary>
        /// <param name="factionEntity"></param>
        /// <param name="colonyEntity"></param>
        public static void Construction(Entity factionEntity, Entity colonyEntity)
        {
            ColonyInfoDB colonyInfo = colonyEntity.GetDataBlob<ColonyInfoDB>();
            InstallationsDB installations = colonyEntity.GetDataBlob<InstallationsDB>();
            var rawMaterialsStockpile = colonyInfo.MineralStockpile;

            var facilityJobs = installations.InstallationJobs;
            float constructionPoints = InstallationAbilityofType(InstallationAbilityType.InstallationConstruction, installations);
            constructionPoints *= BonusesForType(factionEntity, colonyEntity, InstallationAbilityType.InstallationConstruction);
            var faciltiesList = new JDictionary<Guid, double>();

            GenericConstructionJobs(constructionPoints, ref facilityJobs,ref rawMaterialsStockpile, ref faciltiesList);

            foreach (var facilityPair in faciltiesList)
            {
                
                int fullColInstallations = (int)installations.Installations[facilityPair.Key];
                installations.Installations[facilityPair.Key] += (float)facilityPair.Value;
                if ((int)installations.Installations[facilityPair.Key] > fullColInstallations)
                {
                    installations.EmploymentList.Add(new InstallationEmployment 
                    {Enabled = true, Type = facilityPair.Key});
                }
            }

            var refinaryJobs = installations.RefinaryJobs;
            float refinaryPoints = InstallationAbilityofType(InstallationAbilityType.FuelRefinery, installations);
            refinaryPoints *= BonusesForType(factionEntity, colonyEntity, InstallationAbilityType.FuelRefinery);
            var refinedList = new JDictionary<Guid, double>();
            
            GenericConstructionJobs(refinaryPoints, ref refinaryJobs, ref rawMaterialsStockpile, ref refinedList);

            var ordnanceJobs = installations.OrdnanceJobs;
            float ordnancePoints = InstallationAbilityofType(InstallationAbilityType.OrdnanceConstruction, installations);
            ordnancePoints *= BonusesForType(factionEntity, colonyEntity, InstallationAbilityType.OrdnanceConstruction);
            var ordnanceList = new JDictionary<Guid, double>();
            
            GenericConstructionJobs(ordnancePoints, ref ordnanceJobs, ref rawMaterialsStockpile, ref ordnanceList);

            var fighterJobs = installations.FigherJobs;
            float fighterPoints = InstallationAbilityofType(InstallationAbilityType.FighterConstruction, installations);
            fighterPoints *= BonusesForType(factionEntity, colonyEntity, InstallationAbilityType.FighterConstruction);
            var fighterList = new JDictionary<Guid, double>();

            GenericConstructionJobs(fighterPoints, ref fighterJobs, ref rawMaterialsStockpile, ref fighterList);
               
        }

        /// <summary>
        /// an attempt at a more generic constructionProcessor.
        /// should maybe be private.
        /// </summary>
        /// <param name="ablityPointsThisColony"></param>
        /// <param name="jobList"></param>
        /// <param name="rawMaterials"></param>
        /// <param name="stockpileOut"></param>
        public static void GenericConstructionJobs(double ablityPointsThisColony, ref List<ConstructionJob> jobList, ref JDictionary<Guid,int> rawMaterials, ref JDictionary<Guid,double> stockpileOut)
        {
            List<ConstructionJob> newJobList = new List<ConstructionJob>();

            foreach (ConstructionJob job in jobList)
            {
                double pointsToUseThisJob = Math.Min(job.BuildPointsRemaining, (ablityPointsThisColony * job.PriorityPercent.Percent));
                double pointsUsedThisJob = 0;
                //the points per requred resources.
                double pointsPerResourcees = (double)job.BuildPointsRemaining / job.RawMaterialsRemaining.Values.Sum();
                foreach (var resourcePair in new Dictionary<Guid,int>(job.RawMaterialsRemaining))
                {
                    Guid resourceGuid = resourcePair.Key;

                    double pointsPerThisResource = (double)job.BuildPointsRemaining / resourcePair.Value;

                    //maximum rawMaterials needed or availible whichever is less
                    int maxResource = Math.Min(resourcePair.Value, rawMaterials[resourceGuid]);
                    
                    //maximum buildpoints I can use for this resource
                    //should I be using pointsPerResources or pointsPerThisResource?
                    double maxPoint = pointsPerResourcees * maxResource;

                    maxPoint = Math.Min(maxPoint, pointsToUseThisJob); 

                    
                    int usedResource = (int)(maxPoint / pointsPerResourcees);
                    double usedPoints = pointsPerResourcees * usedResource;
                    
                    job.RawMaterialsRemaining[resourceGuid] -= usedResource; //needs to be an int
                    rawMaterials[resourceGuid] -= usedResource; //needs to be an int
                    pointsUsedThisJob += usedPoints;
                    pointsToUseThisJob -= usedPoints;
                                                            
                }
                ablityPointsThisColony -= pointsUsedThisJob;

                double percentPerItem = (double)job.BuildPointsPerItem / 100; 
                double percentthisjob = pointsUsedThisJob / 100; 
                double itemsCreated = percentPerItem * percentthisjob;
                double itemsLeft = job.ItemsRemaining - itemsCreated;
                stockpileOut[job.Type] += job.ItemsRemaining - itemsLeft;

                if (itemsLeft > 0)
                {
                    //recreate constructionJob because it's a struct.
                    ConstructionJob newJob = new ConstructionJob 
                    {
                        Type = job.Type, 
                        ItemsRemaining = (float)itemsLeft, 
                        PriorityPercent = job.PriorityPercent, 
                        RawMaterialsRemaining = job.RawMaterialsRemaining, //check this one. mutability?                    
                        BuildPointsRemaining = job.BuildPointsRemaining - (int)Math.Ceiling(pointsUsedThisJob),
                        BuildPointsPerItem = job.BuildPointsPerItem
                    };
                    newJobList.Add(newJob); //then add it to the new list
                }

            }
            jobList = newJobList; //old list gets replaced with new
        }

        /// <summary>
        /// for changeing the priority of the constructionJobs priorities.
        /// not sure how this should work...
        /// </summary>
        /// <param name="colonyEntity"></param>
        /// <param name="neworder"></param>
        public static void ConstructionPriority(Entity colonyEntity, Message neworder)
        {
            //idk... needs to be something in the message about whether it's Construction, Ordnance or Fighers...  
            //I think if it's a valid list we can just chuck it straight in.
            try
            {
                colonyEntity.GetDataBlob<InstallationsDB>().InstallationJobs = (List<ConstructionJob>)neworder.Data;                
            }
            catch (Exception)
            {
                
                throw;
            }
        }


        /// <summary>
        /// not shure if this should be a whole lot of if statements or if we can tidy it up somewhere else
        /// </summary>
        /// <param name="factioEntity"></param>
        /// <param name="colonyEntity"></param>
        /// <param name="ability"></param>
        /// <returns></returns>
        private static float BonusesForType(Entity factioEntity, Entity colonyEntity, InstallationAbilityType ability )
        {
            //todo link bonuses to type somehow 
            return 1.0f;
        }

        /// <summary>
        /// Returns the total InstallationAbilityPoints on an colony 
        /// it's important that the Employment function is run prior to this,
        /// if any changes have been made to the numer of Installatioins, pop etc etc.
        /// </summary>
        /// <param name="type"></param>
        /// <param name="installationsDB"></param>
        /// <returns></returns>
        private static int InstallationAbilityofType(InstallationAbilityType type, InstallationsDB installationsDB)
        {            
            int totalAbilityValue = 0;
            foreach (KeyValuePair<Guid, int> kvp in installationsDB.WorkingInstallations)
            {
                InstallationSD facility = StaticDataManager.StaticDataStore.Installations[kvp.Key];
                if(facility.BaseAbilityAmounts.ContainsKey(type))
                    totalAbilityValue += facility.BaseAbilityAmounts[type] * kvp.Value;  
            }
            return totalAbilityValue;           
        }
    }
}