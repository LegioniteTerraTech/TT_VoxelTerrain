using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace TT_VoxelTerrain
{
    public class VoxBrushMessage : UnityEngine.Networking.MessageBase
    {
        public VoxBrushMessage()
        {
        }

        public VoxBrushMessage(Vector3 Position, float Radius, float Strength, byte Terrain)
        {
            this.Position = Position;
            this.Radius = Radius;
            this.Strength = Strength;
            this.Terrain = Terrain;
        }

        public override void Deserialize(UnityEngine.Networking.NetworkReader reader)
        {
            Position = reader.ReadVector3();
            Radius = reader.ReadSingle();
            Strength = reader.ReadSingle();
            Terrain = reader.ReadByte();
        }

        public override void Serialize(UnityEngine.Networking.NetworkWriter writer)
        {
            writer.Write(Position);
            writer.Write(Radius);
            writer.Write(Strength);
            writer.Write(Terrain);
        }
        public float Radius, Strength;
        public byte Terrain;
        public Vector3 Position;
    }

}
