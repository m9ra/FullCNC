using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ControllerCNC.Primitives
{
    public class TowerSpeed
    {
        public readonly Speed Speed;

        public readonly bool SpecifiesSpeedUV;

        public bool SpecifiesSpeedXY => !SpecifiesSpeedUV;

        public TowerSpeed(Speed speed, bool specifiesSpeedUV)
        {
            Speed = speed;
            SpecifiesSpeedUV = specifiesSpeedUV;
        }

        public static TowerSpeed From(Speed uv, Speed xy)
        {
            if (uv.StepCount <= xy.StepCount)
                return TowerSpeed.UV(uv);

            return TowerSpeed.XY(xy);
        }

        public static TowerSpeed UV(Speed uv)
        {
            return new TowerSpeed(uv, true);
        }

        public static TowerSpeed XY(Speed xy)
        {
            return new TowerSpeed(xy, false);
        }
    }
}
