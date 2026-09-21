using Autodesk.Revit.DB;

namespace DialuxToRevit.Revit.Geometry
{
    /// <summary>
    /// Unit conversion in one place.
    ///
    /// DIALux writes millimetres and Revit works in decimal feet. Every length
    /// crossing that boundary goes through here, so there is a single place to
    /// look when something lands 25.4 times too far away.
    ///
    /// Named LengthUnits rather than Units because Autodesk.Revit.DB already
    /// has a Units type, and every file here imports that namespace.
    /// </summary>
    public static class LengthUnits
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
