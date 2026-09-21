using Autodesk.Revit.DB;

namespace DialuxToRevit.Revit.Geometry
{
    /// <summary>
    /// Unit conversion in one place.
    ///
    /// DIALux writes millimetres and Revit works in decimal feet. Every length
    /// crossing that boundary goes through here, so there is a single place to
    /// look when something lands 25.4 times too far away.
    /// </summary>
    public static class Units
    {
        public static double MillimetresToFeet(double millimetres)
        {
            return UnitUtils.ConvertToInternalUnits(millimetres, UnitTypeId.Millimeters);
        }

        public static double FeetToMillimetres(double feet)
        {
            return UnitUtils.ConvertFromInternalUnits(feet, UnitTypeId.Millimeters);
        }
    }
}
