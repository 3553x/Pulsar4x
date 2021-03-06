using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Pulsar4X.Vectors;

namespace Pulsar4X.ECSLib.ComponentFeatureSets.Damage
{

    [Flags]
    public enum Connections
    {
        Skin = 1,
        Front = 2,
        Back = 4,
        Sides = 8,
        Structural = 15
    }

    public struct DamageResist
    {
        /*
         this could potentialy get more complex,
         ie transperency, damage passes through but does no damage, probibly need that.
         absorption, and threashold - can absorb heat to a melting point.
         relfection/resistance - will compleatly ignore/reduce damage under a value
        */
        public byte IDCode;
        public int HitPoints;
        //public float Heat;
        //public float Kinetic;
        public float Density;
        
        public DamageResist(byte iDCode, int hitPoints, float density)
        {
            IDCode = iDCode;
            HitPoints = hitPoints;
            //Heat = heat;
            //Kinetic = kinetic;
            Density = density; //kg/m^3
            DamageTools.DamageResistsLookupTable.Add(IDCode, this);
        }
    }
    
    public struct DamageFragment
    {
        public Vector2 Velocity;
        public (int x,int y) Position;
        public float Mass;
        public float Density;//kg/m^3
        public float Length;
    }

    public static class DamageTools
    {
        public static Dictionary<byte, DamageResist> DamageResistsLookupTable = new Dictionary<byte, DamageResist>()
        {
            {0, new DamageResist() {IDCode = 0, HitPoints = 0}} //emptyspace
        };

        public static RawBmp LoadFromBitMap(string file)
        {
            if(!File.Exists(file))
                throw new FileNotFoundException();
            Bitmap bmp = new Bitmap(file, true);
            
            BitmapData lockData = bmp.LockBits(
                new Rectangle(0, 0, bmp.Width, bmp.Height),
                System.Drawing.Imaging.ImageLockMode.ReadWrite, 
                System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            
            // Create an array to store image data
            byte[] imageData = new byte[4 * bmp.Width * bmp.Height];
            // Use the Marshal class to copy image data
            System.Runtime.InteropServices.Marshal.Copy(
                lockData.Scan0, imageData, 0, imageData.Length);
            bmp.UnlockBits(lockData);
            byte[] imageData2 = new byte[4 * bmp.Width * bmp.Height];
            RawBmp newBmp = new RawBmp()
            {
                ByteArray = imageData,
                Depth = 4,
                Height = bmp.Height,
                Width = bmp.Width,
                Stride = 4 * bmp.Width
            };
            return newBmp;
        }

        public static Color FromByte(byte byteColor)
        {
            byte r = byteColor;
            byte g = byteColor;
            byte b = byteColor;
            Color color = Color.FromArgb(255,r, g, b);
            return color;
        }

        public static DamageResist FromColor(Color color)
        {
            byte id = color.R;
            return DamageResistsLookupTable[id];
        }
        
        public static RawBmp CreateComponentByteArray(ComponentDesign componentDesign, byte typeID)
        {
            //cm resolution
            var vol = componentDesign.Volume * 100 / 3;

            int width = (int)Math.Sqrt(vol * componentDesign.AspectRatio);
            int height = (int)(componentDesign.AspectRatio * width);
            int v2d = height * width;

            if (componentDesign.AspectRatio > 1)
            {
                width = (int)(width / componentDesign.AspectRatio);
                height = (int)(height / componentDesign.AspectRatio);
            }

            int depth = 4;
            int size = depth * width * height;
            int stride = width * depth;
            
            byte[] buffer = new byte[size];

            for (int ix = 0; ix < width; ix++)
            {
                for (int iy = 0; iy < height; iy++)
                {
                    byte c = typeID;
                    RawBmp.SetPixel(ref buffer, stride, depth, ix, iy, 255, 255,c, 255);
                }
            }

            RawBmp bmp = new RawBmp()
            {
                ByteArray =  buffer,
                Stride = stride,
                Depth =  4,
                Width = width,
                Height = height,
                
            };
            return bmp;
        }


        public static List<RawBmp> DealDamage(EntityDamageProfileDB damageProfile, DamageFragment damage)
        {
            RawBmp shipDamageProfile = damageProfile.DamageProfile;

            List<RawBmp> damageFrames = new List<RawBmp>();

            var fragmentMass = damage.Mass;
            var dpos = damage.Position;
            var dvel = damage.Velocity;
            var dden = damage.Density;
            var dlen = damage.Length;
            var pos = new Vector2(dpos.x, dpos.y);
            var pixelscale = 0.01;
            double startMomentum = dvel.Length() * fragmentMass;
            double momentum = startMomentum; 
            
            byte[] byteArray = new byte[shipDamageProfile.ByteArray.Length];
            Buffer.BlockCopy(shipDamageProfile.ByteArray, 0, byteArray, 0, shipDamageProfile.ByteArray.Length);
            RawBmp firstFrame = new RawBmp()
            {
                ByteArray = byteArray,
                Height = shipDamageProfile.Height,
                Width = shipDamageProfile.Width,
                Depth = shipDamageProfile.Depth,
                Stride = shipDamageProfile.Stride
            };
            damageFrames.Add(firstFrame);
            (byte r, byte g, byte b, byte a) savedpx = shipDamageProfile.GetPixel(dpos.x, dpos.y);
            (int x, int y) savedpxloc = dpos;
            
            while (momentum > 0 && dpos.x >= 0 && dpos.x <= shipDamageProfile.Width && dpos.y >= 0 && dpos.y <= shipDamageProfile.Height)
            {
                byteArray = new byte[shipDamageProfile.ByteArray.Length];
                RawBmp lastFrame = damageFrames.Last();
                Buffer.BlockCopy(lastFrame.ByteArray, 0, byteArray, 0, shipDamageProfile.ByteArray.Length);
                var thisFrame = new RawBmp()
                {
                    ByteArray = byteArray,
                    Height = shipDamageProfile.Height,
                    Width = shipDamageProfile.Width,
                    Depth = shipDamageProfile.Depth,
                    Stride = shipDamageProfile.Stride
                };

                (byte r, byte g, byte b, byte a) px = thisFrame.GetPixel(dpos.x, dpos.y);
                if (px.a > 0)
                {
                    DamageResist damageresist = DamageResistsLookupTable[px.r];

                    double density = damageresist.Density / (px.a / 255f); //density / health
                    double maxImpactDepth = dlen * dden / density;
                    double depthPercent = pixelscale / maxImpactDepth;
                    dlen -= (float)(damage.Length * depthPercent);
                    var momentumLoss = startMomentum * depthPercent;
                    momentum -= momentumLoss;
                    if (momentum > 0)
                    {
                        px = ( px.r, px.g, px.b, 0);
                        damageProfile.ComponentLookupTable[px.g].HTKRemaining -= 1;
                    }

                }

                thisFrame.SetPixel(dpos.x, dpos.y, byte.MaxValue, byte.MaxValue, byte.MaxValue, (byte)fragmentMass);
                thisFrame.SetPixel(savedpxloc.x, savedpxloc.y, savedpx.r, savedpx.g, savedpx.b, savedpx.a);
                damageFrames.Add(thisFrame);
                savedpxloc = dpos;
                savedpx = px;


                double dt = 1 / dvel.Length(); 
                pos.X += dvel.X * dt;
                pos.Y += dvel.Y * dt;
                dpos.x = Convert.ToInt32(pos.X);
                dpos.y = Convert.ToInt32(pos.Y);
            }

            
            Buffer.BlockCopy(damageFrames.Last().ByteArray, 0, byteArray, 0, shipDamageProfile.ByteArray.Length);
            var finalFrame = new RawBmp()
            {
                ByteArray = byteArray,
                Height = shipDamageProfile.Height,
                Width = shipDamageProfile.Width,
                Depth = shipDamageProfile.Depth,
                Stride = shipDamageProfile.Stride
            };
            finalFrame.SetPixel(savedpxloc.x, savedpxloc.y, savedpx.r, savedpx.g, savedpx.b, savedpx.a);
            return damageFrames;
        }
    }
}