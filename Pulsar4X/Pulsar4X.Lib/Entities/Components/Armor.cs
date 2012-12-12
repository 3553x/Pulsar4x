using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Pulsar4X.Entities;
using Pulsar4X.Entities.Components;
using log4net;
using System.ComponentModel;

/// <summary>
/// This is a duplicate of the Aurora armor system: Ships are basically spheres with a volume of HullSize.
/// Armor is coated around these spheres in ever increasing amounts per armor depth. likewise armor has to cover itself, as well as the ship sphere.
/// ShipOnDamage(or its equivilant) will touch on this, though I have yet to figure that out.
/// This is certainly up for updating/revision. - NathanH.
/// </summary>

namespace Pulsar4X.Entities.Components
{
    /// <summary>
    /// Ship Class armor definition. each copy of a ship will point to their shipclass, which points to this for important and hopefully static data.
    /// </summary>
	public class ArmorDefTN
	{
        /// <summary>
        /// Armor coverage of surface area of the ship per HullSpace(50.0 ton increment). This will vary with techlevel and can be updated. CalcArmor requires this.
        /// </summary>
        private ushort ArmorPerHS;
        public ushort armorPerHS
        {
            get { return ArmorPerHS; }
        }

        /// <summary>
        /// Number of armor layers, CalcArmor needs to know this as well.
        /// </summary>
        private ushort Depth;
        public ushort depth
        {
            get { return Depth; }
        }

        /// <summary>
        /// Overall size of the armor, this is added to the ship proper. CalcArmor calculates this.
        /// </summary>
        private double Size;
        public double size
        {
            get { return Size; }
        }
        
        /// <summary>
        /// Area coverage of the armor, Cost and column # both require this.
        /// </summary>
        private double Area;
        public double area
        {
            get { return Area; }
        }

        /// <summary>
        /// Cost of the Armor. Ship costs, repair cost, and material resource costs will depend on this. It is naively equal to area in terms of per numbers, but resources required
        /// will vary with tech level. In Aurora costs shift from duranium to neutronium for example.
        /// </summary>
        private decimal Cost;
        public decimal cost
        {
            get { return Cost; }
        }

        /// <summary>
        /// Column number counts how many columns of armor this ship can have, and hence how well protected it is from normal damage.
        /// This is determined by taking the overall strength requirement divided by the depth of the armor.
        /// </summary>
        private ushort CNum;
        public ushort cNum
        {
            get { return CNum; }
        }

        /// <summary>
        /// Just an empty constructor. I don't really need this, the main show is in CalcArmor.
        /// </summary>
	    public ArmorDefTN()
	    {
		    Size = 0.0;
		    Cost = 0.0m;
		    Area = 0.0;
	    }

        /// <summary>
        /// CalcArmor takes the size of the craft, as well as armor tech level and requested depth, and calculates how big the armor must be to cover the ship.
        /// this is an iterative process, each layer of armor has to be placed on top of the old layer. Aurora updates this every time a change is made to the ship,
        /// and I have written this function to work in the same manner.
        /// </summary>
        /// <param name="armorPerHS"> armor Per Unit of Hull Space </param>
        /// <param name="sizeOfCraft"> In HullSpace increments </param>
        /// <param name="armorDepth"> Armor Layers </param>
	    public void CalcArmor(ushort armorPerHS, double sizeOfCraft, ushort armorDepth)
	    {
            /// <summary>
            /// Bounds checking as armorDepth is a short
            if (armorDepth < 1)
                armorDepth = 1;
            if (armorDepth > 65535)
                armorDepth = 65535;

		    ArmorPerHS = armorPerHS;
		    Depth = armorDepth;

            /// <summary>
            /// Armor calculation is as follows:
            /// First Volume of a sphere: V = 4/3 * pi * r^3 r^3 = 3V/4pi. Hullsize is the value for volume. radius is what needs to be determined
            /// From radius the armor area can be derived: A = 4 * pi * r^2
            /// Area / 4.0 is the required strength area that needs to be covered.
            /// </summary>

            bool done;
		    int loop;
		    double volume,radius3,radius2,radius,area=0.0, strengthReq=0.0,lastPro;
		    double areaF;
		    double temp1 = 1.0 / 3.0;	
		    double pi = 3.14159654;		

            /// <summary>
            /// Size must be initialized to 0.0 for this
            /// Armor is being totally recalculated every time this is run, the previous result is thrown out.
            /// </summary>
		    Size = 0.0; 

            /// <summary>
            /// For each layer of Depth.
            /// </summary>
		    for( loop = 0; loop < Depth; loop++) 
		    {
			    done = false;
			    lastPro = -1;
	    		volume = Math.Ceiling( sizeOfCraft + Size );
		
                /// <summary>
                /// While Armor does not yet fully cover the ship and itself.
                /// </summary>
			    while( done == false ) 
			    {
				    radius3 = ( 3.0 * volume ) / ( 4.0 * pi ) ;
				    radius = Math.Pow( radius3, temp1 );
				    radius2 = Math.Pow( radius, 2.0 );
				    area = ( 4.0 * pi ) * radius2;

				    areaF = Math.Floor( area * 10.0 ) / 10.0;
				    area = ( Math.Round(area * 10.0) ) / 10.0;
				    area *= (double)( loop + 1 );
				    strengthReq = area / 4.0 ;

				    Size = Math.Ceiling( ( strengthReq / (double) ArmorPerHS ) * 10.0 ) / 10.0;
				    volume = Math.Ceiling(sizeOfCraft + Size);

				    if( Size == lastPro )
					    done = true;

				    lastPro = Size;
			    }
		    }

		    Area = ( area * Depth ) / 4.0;
		    Cost = (decimal)Area;
		    CNum = (ushort)Math.Floor( strengthReq / (double)Depth );
	    }
        /// <summary>
        /// End of Function CalcArmor
        /// </summary>
    }
    /// <summary>
    /// End of Class ArmorDefTN
    /// </summary>
    
    /// <summary>
    /// Armor contains ship data itself. each ship will have its own copy of this.
    /// </summary>
    public class ArmorTN
    {
        /// <summary>
        /// isDamaged controls whether or not armorColumns has been populated yet. 
        /// </summary>
        private bool IsDamaged;
        public bool isDamaged
        {
            get { return IsDamaged; }
        }

        /// <summary>
        /// armorColumns contains the actual data that will need to be looked up
        /// </summary>
        private BindingList<ushort> ArmorColumns;
        public BindingList<ushort> armorColumns
        {
            get { return ArmorColumns; }
        }

        /// <summary>
        /// armorDamage is an easily stored listing of the damage that the ship has taken
        /// Column # is the key, and value is how much damage has been done to that column( DepthValue to Zero ).
        /// </summary>
        private Dictionary<ushort, ushort> ArmorDamage;
        public Dictionary<ushort, ushort> armorDamage
        {
            get { return ArmorDamage; }
        }

        /// <summary>
        /// ArmorDef contains the definitions for this component
        /// </summary>
        private ArmorDefTN ArmorDef;
        public ArmorDefTN armorDef
        {
            get { return ArmorDef; }
        }

        /// <summary>
        /// the actual ship armor constructor does nothing with armorColumns or armorDamage yet.
        /// </summary>
        public ArmorTN(ArmorDefTN protectionDef)
        {
            IsDamaged = false;
            ArmorColumns = new BindingList<ushort>();
            ArmorDamage = new Dictionary<ushort, ushort>();
            ArmorDef = protectionDef;
        }

        /// <summary>
        /// SetDamage puts (CurrentDepth-DamageValue) damage into a specific column.
        /// </summary>
        /// <param name="ColumnCount">Total Columns, ship will have access to ship class which has armorDef.</param>
        /// <param name="Depth">Full and pristine armor Depth.</param>
        /// <param name="Column">The specific column to be damaged.</param>
        /// <param name="DamageValue">How much damage has been done.</param>
        public void SetDamage(ushort ColumnCount, ushort Depth, ushort Column, ushort DamageValue)
        {
            ushort newDepth;
            if (IsDamaged == false)
            {
                for (ushort loop = 0; loop < ColumnCount; loop++)
                {
                    if (loop != Column)
                    {
                        ArmorColumns.Add(Depth);
                    }
                    else
                    {
                        /// <summary>
                        /// I have to type cast this subtraction of a short from a short into a short with a short.
                        /// </summary>
                        newDepth = (ushort)(Depth - DamageValue);
                        if (newDepth < 0)
                            newDepth = 0;

                        ArmorColumns.Add(newDepth);
                        ArmorDamage.Add(Column, newDepth);
                    }
                }
                /// <summary>
                /// end for ColumnCount
                /// </summary>
                IsDamaged = true;
            }
            /// <summary>
            /// end if isDamaged = false
            /// </summary>
            else
            {
                newDepth = (ushort)(ArmorColumns[Column] - DamageValue);
                ArmorColumns[Column] = newDepth;

                if (ArmorDamage.ContainsKey(Column) == true)
                {
                    ArmorDamage[Column] = newDepth;
                }
                else
                {
                    ArmorDamage.Add(Column, newDepth);
                }
            }
            /// <summary>
            /// end else if isDamaged = true
            /// </summary>

        }

        /// <summary>
        /// RepairSingleBlock undoes one point of damage from the worst damaged column.
        /// If this totally fixes the column all damage to that column is repaired and it is removed from the list.
        /// If all damage overall is repaired isDamaged is set to false, and the armorColumn is depopulated.
        /// </summary>
        /// <param name="Depth">Armor Depth, this will be called from ship which will have access to ship class and therefore this number</param>
        public void RepairSingleBlock(ushort Depth)
        {
            ushort mostDamaged = ArmorDamage.Min().Key;

            ushort repair = (ushort)(ArmorDamage.Min().Value + 1);
            ArmorDamage[mostDamaged] = repair;
            ArmorColumns[mostDamaged] = repair;

            if (ArmorDamage[mostDamaged] == Depth)
            {
                ArmorDamage.Remove(mostDamaged);

                if (ArmorDamage.Count == 0)
                {
                    RepairAllArmor();
                }
            }
        }

        /// <summary>
        /// When the armor of a ship is repaired at a shipyard all damage is cleared.
        /// Also convienently called from RepairSingleBlock if a hangar manages to complete all repairs.
        /// </summary>
        public void RepairAllArmor()
        {
            IsDamaged = false;
            ArmorDamage.Clear();
            ArmorColumns.Clear();
        }
    }
    /// <summary>
    /// End of Class ArmorTN
    /// </summary>
}
/// <summary>
/// End of Namespace Pulsar4X.Entites.Components
/// </summary>