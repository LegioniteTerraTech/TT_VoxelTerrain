using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace TT_VoxelTerrain
{
    /// <summary>
    /// Works kinda like Astroneer's rover tools
    /// </summary>
    internal class DiggerTank
    {
        private Tank tank;
        private bool dirtyWheels = false;
        private List<ModuleWheels> wheels = new List<ModuleWheels>();
        private int furthestForwards = int.MinValue;
        public float GetForwardsWheelAltitude()
        {
            tank.transform.
        }
        public void Init(Tank tank)
        {
            this.tank = tank;
            tank.AttachEvent.Subscribe(RefreshWheels);
            tank.DetachEvent.Subscribe(RefreshWheels);
        }

        public void RefreshWheels(TankBlock block, Tank tank) => dirtyWheels = true;

        public void FixedUpdate()
        {
            if (dirtyWheels)
            {
                dirtyWheels = false;
                wheels.Clear();
                Quaternion toCabRot = Quaternion.Inverse(tank.rootBlockTrans.localRotation);
                foreach (var wheel in tank.blockman.IterateBlockComponents<ModuleWheels>())
                {
                    TankBlock blockInst = wheel.block;
                    wheels.Add(wheel);
                    foreach (var wheel2 in blockInst.filledCells)
                    {
                        float fwdDist = (toCabRot * (blockInst.trans.localPosition + (blockInst.trans.localRotation * wheel2))).z;
                        if (fwdDist > furthestForwards)
                            furthestForwards = Mathf.CeilToInt(fwdDist);
                    }
                }
            }
        }
    }
}
