﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Pulsar4X.Entities.Components;


namespace Pulsar4X.Entities.Components
{
    public class CIWSDefTN : ComponentDefTN
    {
        /// <summary>
        /// Firing rate of this CIWS.
        /// </summary>
        private int GaussFireRateTech;
        public int gaussFireRateTech
        {
            get { return GaussFireRateTech; }
        }

        /// <summary>
        /// BFC distance modifier, effects internal BFC size, better FCs are smaller.
        /// </summary>
        private int BeamFCRangeTech;
        public int beamFCRangeTech
        {
            get { return BeamFCRangeTech; }
        }

        /// <summary>
        /// BFC Tracking modifier, effects tracking obviously, and also internal turret size.
        /// </summary>
        private int BeamFCTrackingTech;
        public int beamFCTrackingTech
        {
            get { return BeamFCTrackingTech; }
        }

        /// <summary>
        /// Sensor tech modifier, better sensors are smaller, but all sensors must have the capability of spotting a missile at atleast 10k km as per whatever internal formula I am not 
        /// entirely aware of.
        /// </summary>
        private int ActiveStrengthTech;
        public int activeStrengthTech
        {
            get { return ActiveStrengthTech; }
        }

        /// <summary>
        /// Turret gear size modifier. While each gear remains a constant 0.5 in size, they get better at adding to tracking with each tech.
        /// </summary>
        private int TurretGearTech;
        public int turretGearTech
        {
            get { return TurretGearTech; }
        }

        /// <summary>
        /// ECCM technology on this CIWS. all ECCMs are a constant 0.5 in size, but better ones do more to counteract ECM.
        /// </summary>
        private int ECCMTech;
        public int eccmTech
        {
            get { return ECCMTech; }
        }

        /// <summary>
        /// rate of fire for this CIWS, it will be gauss fire rate * 2.
        /// </summary>
        private int ROF;
        public int rOF
        {
            get { return ROF; }
        }

        private int Tracking;
        public int tracking
        {
            get { return Tracking; }
        }

        /// <summary>
        /// ECCM strength of this CIWS.
        /// </summary>
        private int ECCM;
        public int eCCM
        {
            get { return ECCM; }
        }

        //Gauss cannon is a dual fire 50% tohit chance weapon
        /// <summary>
        /// Constructor for the Close In Weapon System(TN) Definition.
        /// </summary>
        /// <param name="Title">Name of the CIWS</param>
        /// <param name="GaussFireRateTech">Fire rate of the basic gauss turret this is built off of.</param>
        /// <param name="BFCRangeTech">How large or small will the fire control be? better tech = smaller</param>
        /// <param name="BFCTrackTech">How good at tracking will this CIWS be, and how many gears are required?</param>
        /// <param name="ActiveStrTech">how large or small will the internal sensor be?</param>
        /// <param name="TurretTech">Turret gear capability</param>
        /// <param name="ECCM_Tech">is ECCM present, and if so how strong is it?</param>
        public CIWSDefTN(String Title, int GaussROFTech, int BFCRangeTech, int BFCTrackTech, int ActiveStrTech, int TurretTech, int ECCM_Tech)
        {
            Name = Title;
            Id = Guid.NewGuid();

            componentType = ComponentTypeTN.CIWS;

            GaussFireRateTech = GaussROFTech;
            BeamFCRangeTech = BFCRangeTech;
            BeamFCTrackingTech = BFCTrackTech;
            ActiveStrengthTech = ActiveStrTech;
            TurretGearTech = TurretTech;
            ECCMTech = ECCM_Tech;

            /// <summary>
            /// This is considered a dual turret, so firing rate is GaussShots * 2.
            /// </summary>
            ROF = Constants.BeamWeaponTN.GaussShots[GaussFireRateTech] * 2;

            /// <summary>
            /// Tracking is Base * 4
            /// </summary>
            Tracking = (int)(Constants.BFCTN.BeamFireControlTracking[BFCTrackTech] * 4);

            /// <summary>
            /// Gear count factors into turret size.
            /// </summary>
            float GearCount = (float)Tracking / Constants.BFCTN.BeamFireControlTracking[TurretTech];

            /// <summary>
            /// FC range must be 20,000 to ensure 100% accuracy at 10,000 for final defensive fire. size is 1.0 / FCfactor.
            /// this variable should be between 8.75(maximum) to 0.5(minimum). The BFC has to be able to fire at 10k km at atleast 50%.
            /// </summary>
            float FCfactor = ((float)Constants.BFCTN.BeamFireControlRange[BeamFCRangeTech] * 1000.0f) / 20000.0f;  

            /// <summary>
            /// base size is 5.0 HS.
            /// </summary>
            size = 5.0f;
            
            /// <summary>
            /// Gear Size is 0.5.
            /// </summary>
            size = (GearCount * 0.5f);

            /// <summary>
            /// BFC size is 1 / FCfactor.
            /// </summary>
            size = size + (1.0f / FCfactor);

            /// <summary>
            /// The sensor size is 3.0 / Active sensor strength.
            /// Presumably this is the minimum sensor needed to spot something at 10k km, which is far more permissive than Final defensive fire.
            /// </summary>
            size = size + (3.0f / (float)Constants.SensorTN.ActiveStrength[ActiveStrTech]);

            /// <summary>
            /// ECCM is always 0.5, and ECCM strength is otherwise normal. Again it looks like CIWS have it better than any other component.
            /// </summary>
            if (ECCMTech != 0)
            {
                size = size + 0.5f;
                ECCM = ECCMTech * 10;
            }
            else
            {
                ECCM = 0;
            }

            /// <summary>
            /// Crew is size in HS rounded up.
            /// </summary>
            crew = (byte)Math.Ceiling(size);

            /// <summary>
            /// HTK is the floor of size / 3.
            /// </summary>
            htk = (byte)Math.Floor(size / 3.0f);

            /// <summary>
            /// base cost of 3
	        /// cost + 5 per gauss level
	        /// cost + (Turret tracking gear / 100 + 5)
	        /// cost + 2.5 per ECCM level
            /// </summary>
            float costFactor = ((float)Constants.BFCTN.BeamFireControlTracking[TurretTech] / 100.0f) + 5.0f;
	        cost = (decimal)( 3.0f + ((float)ROF * 5.0f) + ((float)GearCount * 0.5f * costFactor ) + ((float)ECCM * 2.5f ));


            isSalvaged = false;
            isObsolete = false;
            isMilitary = false;
            isDivisible = false;
            isElectronic = false;
        }

    }

    public class CIWSTN : ComponentTN
    {
        /// <summary>
        /// Definition for the Close in Weapon System.
        /// </summary>
        private CIWSDefTN CIWSDef;
        public CIWSDefTN cIWSDef
        {
            get { return CIWSDef; }
        }


        /// <summary>
        /// Constructor for the close in weapon system component.
        /// </summary>
        /// <param name="definition">definition on which this component is based</param>
        public CIWSTN(CIWSDefTN definition)
        {
            CIWSDef = definition;

            Name = CIWSDef.Name;

            isDestroyed = false;
        }
    }
}