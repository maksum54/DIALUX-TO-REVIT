using System;
using System.Globalization;

namespace DialuxToRevit.Core.Model
{
    /// <summary>Identifies one storey of one building within an export.</summary>
    public struct StoreyKey : IEquatable<StoreyKey>
    {
        public readonly int Building;
        public readonly int Floor;

        public StoreyKey(int building, int floor)
        {
            Building = building;
            Floor = floor;
        }

        public bool Equals(StoreyKey other)
        {
            return Building == other.Building && Floor == other.Floor;
        }

        public override bool Equals(object obj)
        {
            return obj is StoreyKey && Equals((StoreyKey)obj);
        }

        public override int GetHashCode()
        {
            return (Building * 397) ^ Floor;
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "BLD{0}_FL{1}", Building, Floor);
        }
    }
}
