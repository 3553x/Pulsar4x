﻿using System;
using System.Collections.Generic;
using System.Linq;


namespace Pulsar4X.ECSLib
{
    public static class ConstructionProcessor
    {
        internal static void ConstructStuff(Entity colony, Game game)
        {
            Dictionary<Guid, int> mineralStockpile = colony.GetDataBlob<ColonyInfoDB>().mineralStockpile;
            Dictionary<Guid, int> materialStockpile = colony.GetDataBlob<ColonyInfoDB>().refinedStockpile;
            Dictionary<Guid, int> componentStockpile = colony.GetDataBlob<ColonyInfoDB>().componentStockpile;

            var colonyConstruction = colony.GetDataBlob<ColonyConstructionDB>();
            var factionInfo = colony.GetDataBlob<OwnedDB>().ObjectOwner.GetDataBlob<FactionInfoDB>();


            var pointRates = new Dictionary<IndustryType, int>(colonyConstruction.ConstructionRates);
            int maxPoints = colonyConstruction.PointsPerTick;

            List<ConstructionJob> constructionJobs = colonyConstruction.JobBatchList;
            foreach (ConstructionJob batchJob in constructionJobs.ToArray())
            {
                var designInfo = factionInfo.ComponentDesigns[batchJob.ItemGuid].GetDataBlob<ComponentInfoDB>();
                IndustryType conType = batchJob.IndustryType;
                //total number of resources requred for a single job in this batch
                int resourcePoints = designInfo.MinerialCosts.Sum(item => item.Value);
                resourcePoints += designInfo.MaterialCosts.Sum(item => item.Value);
                resourcePoints += designInfo.ComponentCosts.Sum(item => item.Value);

                while ((pointRates[conType] > 0) && (maxPoints > 0) && (batchJob.NumberCompleted < batchJob.NumberOrdered))
                {
                    //gather availible resorces for this job.
                    
                    ConsumeResources(mineralStockpile, batchJob.MineralsRequired);
                    ConsumeResources(materialStockpile, batchJob.MaterialsRequired);
                    ConsumeResources(componentStockpile, batchJob.ComponentsRequired);

                    int useableResourcePoints = designInfo.MinerialCosts.Sum(item => item.Value) - batchJob.MineralsRequired.Sum(item => item.Value);
                    useableResourcePoints += designInfo.MaterialCosts.Sum(item => item.Value) - batchJob.MaterialsRequired.Sum(item => item.Value);
                    useableResourcePoints += designInfo.ComponentCosts.Sum(item => item.Value) - batchJob.ComponentsRequired.Sum(item => item.Value);
                    //how many construction points each resourcepoint is worth.
                    int pointPerResource = designInfo.BuildPointCost / resourcePoints;
                    
                    //calculate how many construction points each resource we've got stored for this job is worth
                    int pointsToUse = Math.Min(pointRates[conType], maxPoints);
                    pointsToUse = Math.Min(pointsToUse, batchJob.PointsLeft);
                    pointsToUse = Math.Min(pointsToUse, useableResourcePoints * pointPerResource);
                    
                    //construct only enough for the amount of resources we have. 
                    batchJob.PointsLeft -= pointsToUse;
                    pointRates[conType] -= pointsToUse;                    
                    maxPoints -= pointsToUse;

                    if (batchJob.PointsLeft == 0)
                    {
                        BatchJobItemComplete(colony, batchJob,designInfo);
                    }
                }
            }
        }

        private static void BatchJobItemComplete(Entity colonyEntity, ConstructionJob batchJob, ComponentInfoDB designInfo)
        {
            var colonyConstruction = colonyEntity.GetDataBlob<ColonyConstructionDB>();
            batchJob.NumberCompleted++;
            batchJob.PointsLeft = designInfo.BuildPointCost;
            batchJob.MineralsRequired = designInfo.MinerialCosts;
            batchJob.MineralsRequired = designInfo.MaterialCosts;
            batchJob.MineralsRequired = designInfo.ComponentCosts;
            if (batchJob.IndustryType == IndustryType.Installations)
            {
                var factionInfo = colonyEntity.GetDataBlob<OwnedDB>().ObjectOwner.GetDataBlob<FactionInfoDB>();
                Entity facilityDesignEntity = factionInfo.ComponentDesigns[batchJob.ItemGuid];
                var colonyInfo = colonyEntity.GetDataBlob<ColonyInfoDB>();
                colonyInfo.Installations.SafeValueAdd(facilityDesignEntity,1);
                ReCalcProcessor.ReCalcAbilities(colonyEntity);
            }


            if (batchJob.NumberCompleted == batchJob.NumberOrdered)
            {
                colonyConstruction.JobBatchList.Remove(batchJob);
                if (batchJob.Auto)
                {
                    colonyConstruction.JobBatchList.Add(batchJob);
                }
            }
        }

        private static void ConsumeResources(IDictionary<Guid, int> stockpile, IDictionary<Guid, int> toUse)
        {   
        
            foreach (KeyValuePair<Guid, int> kvp in toUse.ToArray())
            {
                if (stockpile.ContainsKey(kvp.Key))
                {
                    int amountUsedThisTick = Math.Min(kvp.Value, toUse[kvp.Key]);
                    toUse[kvp.Key] -= amountUsedThisTick;
                    stockpile[kvp.Key] -= amountUsedThisTick;
                }
            }         
        }

        /// <summary>
        /// called by ReCalcProcessor
        /// </summary>
        /// <param name="colonyEntity"></param>
        public static void ReCalcConstructionRate(Entity colonyEntity)
        {
            List<Entity> installations = colonyEntity.GetDataBlob<ColonyInfoDB>().Installations.Keys.ToList();
            var factories = new List<Entity>();
            foreach (Entity inst in installations)
            {
                if (inst.HasDataBlob<IndustryAbilityDB>())
                    factories.Add(inst);
            }

            var typeRate = new Dictionary<IndustryType, int>{
                {IndustryType.Ordnance, 0}, 
                {IndustryType.Installations, 0}, 
                {IndustryType.Fighters, 0},
                {IndustryType.ComponentConstruction, 0},
                {IndustryType.Ships, 0},
            };

            foreach (Entity factory in factories)
            {
                if (factory.HasDataBlob<IndustryAbilityDB>())
                {
                    var constructionAbilityDB = factory.GetDataBlob<IndustryAbilityDB>();
                    foreach (KeyValuePair<IndustryType, int> keyValuePair in typeRate)
                    {
                        IndustryType currentType = keyValuePair.Key;
                        typeRate[currentType] += constructionAbilityDB.GetIndustryRate(currentType);
                    }
                }
            }
            colonyEntity.GetDataBlob<ColonyConstructionDB>().ConstructionRates = typeRate;
            int maxPoints = 0;
            foreach (int p in typeRate.Values)
            {
                if (p > maxPoints)
                    maxPoints = p;
            }
            colonyEntity.GetDataBlob<ColonyConstructionDB>().PointsPerTick = maxPoints;
        }


        #region PlayerInteraction

        /// <summary>
        /// Adds a job to a colonys ColonyConstructionDB.JobBatchList
        /// </summary>
        /// <param name="colonyEntity"></param>
        /// <param name="job"></param>
        [PublicAPI]
        public static void AddJob(Entity colonyEntity, ConstructionJob job)
        {
            var constructingDB = colonyEntity.GetDataBlob<ColonyConstructionDB>();
            var factionInfo = colonyEntity.GetDataBlob<OwnedDB>().ObjectOwner.GetDataBlob<FactionInfoDB>();
            lock (constructingDB.JobBatchList) //prevent threaded race conditions
            {
                //check that this faction does have the design on file. I *think* all this type of construction design will get stored in factionInfo.ComponentDesigns
                if (factionInfo.ComponentDesigns.ContainsKey(job.ItemGuid))
                    constructingDB.JobBatchList.Add(job);
            }
        }

        
        /// <summary>
        /// Moves a job up or down the ColonyRefiningDB.JobBatchList. 
        /// </summary>
        /// <param name="colonyEntity">the colony that's being interacted with</param>
        /// <param name="job">the job that needs re-prioritising</param>
        /// <param name="delta">How much to move it ie: 
        /// -1 moves it down the list and it will be done later
        /// +1 moves it up the list andit will be done sooner
        /// this will safely handle numbers larger than the list size, 
        /// placing the item either at the top or bottom of the list.
        /// </param>
        [PublicAPI]
        public static void ChangeJobPriority(Entity colonyEntity, ConstructionJob job, int delta)
        {
            var constructingDB = colonyEntity.GetDataBlob<ColonyConstructionDB>();
            lock (constructingDB.JobBatchList) //prevent threaded race conditions
            {
                //first check that the job does still exsist in the list.
                if (constructingDB.JobBatchList.Contains(job))
                {
                    var currentIndex = constructingDB.JobBatchList.IndexOf(job);
                    var newIndex = currentIndex + delta;
                    if (newIndex <= 0)
                    {
                        constructingDB.JobBatchList.RemoveAt(currentIndex);
                        constructingDB.JobBatchList.Insert(0, job);
                    }
                    else if (newIndex >= constructingDB.JobBatchList.Count - 1)
                    {
                        constructingDB.JobBatchList.RemoveAt(currentIndex);
                        constructingDB.JobBatchList.Add(job);
                    }
                    else
                    {
                        constructingDB.JobBatchList.RemoveAt(currentIndex);
                        constructingDB.JobBatchList.Insert(newIndex, job);
                    }
                }
            }
        } 
        #endregion
    }
}