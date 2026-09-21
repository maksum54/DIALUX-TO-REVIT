using System;
using Autodesk.Revit.DB;

namespace DialuxToRevit.Revit.Geometry
{
    /// <summary>
    /// Maps DIALux plan coordinates onto Revit model coordinates.
    ///
    /// The export's origin is whatever DIALux happened to use, which is rarely
    /// the project origin -- the reference export has all-negative Y, for
    /// instance. Only X and Y are transformed here; elevation comes from the
    /// chosen level and offset instead, because the level is what the user
    /// actually wants the fixture associated with.
    /// </summary>
    public sealed class CoordinateTransform
    {
        private readonly double _cos;
        private readonly double _sin;
        private readonly double _originX;
        private readonly double _originY;
        private readonly double _offsetX;
        private readonly double _offsetY;

        private CoordinateTransform(double rotationRadians,
            double originX, double originY, double offsetX, double offsetY)
        {
            RotationRadians = rotationRadians;
            _cos = Math.Cos(rotationRadians);
            _sin = Math.Sin(rotationRadians);
            _originX = originX;
            _originY = originY;
            _offsetX = offsetX;
            _offsetY = offsetY;
        }

        /// <summary>
        /// Rotation applied to the plan, in radians. Fixture rotations from the
        /// export are turned by this as well, so a rotated alignment keeps the
        /// luminaires pointing the way they do in DIALux.
        /// </summary>
        public double RotationRadians { get; }

        /// <summary>The DIALux and Revit origins already coincide.</summary>
        public static CoordinateTransform OriginToOrigin()
        {
            return new CoordinateTransform(0.0, 0.0, 0.0, 0.0, 0.0);
        }

        /// <summary>A translation and rotation entered by hand, in millimetres and degrees.</summary>
        public static CoordinateTransform Manual(double dxMillimetres, double dyMillimetres,
            double rotationDegrees)
        {
            return new CoordinateTransform(
                rotationDegrees * Math.PI / 180.0,
                0.0,
                0.0,
                LengthUnits.MillimetresToFeet(dxMillimetres),
                LengthUnits.MillimetresToFeet(dyMillimetres));
        }

        /// <summary>
        /// Alignment from two reference points: the user picks two points in
        /// Revit and gives the matching DIALux coordinates. This is the option
        /// to reach for on a real project, where the two coordinate systems are
        /// unrelated.
        /// </summary>
        /// <param name="sourceA">First DIALux point, millimetres.</param>
        /// <param name="sourceB">Second DIALux point, millimetres.</param>
        /// <param name="targetA">Matching Revit point, internal units.</param>
        /// <param name="targetB">Matching Revit point, internal units.</param>
        public static CoordinateTransform TwoPoint(
            UV sourceA, UV sourceB, XYZ targetA, XYZ targetB)
        {
            if (sourceA == null || sourceB == null || targetA == null || targetB == null)
            {
                throw new ArgumentNullException(nameof(sourceA));
            }

            double sourceAx = LengthUnits.MillimetresToFeet(sourceA.U);
            double sourceAy = LengthUnits.MillimetresToFeet(sourceA.V);
            double sourceBx = LengthUnits.MillimetresToFeet(sourceB.U);
            double sourceBy = LengthUnits.MillimetresToFeet(sourceB.V);

            double sourceAngle = Math.Atan2(sourceBy - sourceAy, sourceBx - sourceAx);
            double targetAngle = Math.Atan2(targetB.Y - targetA.Y, targetB.X - targetA.X);

            return new CoordinateTransform(
                targetAngle - sourceAngle,
                sourceAx,
                sourceAy,
                targetA.X,
                targetA.Y);
        }

        /// <summary>
        /// Distance between the two reference pairs, as a ratio.
        ///
        /// It should be 1. Anything else means the two points do not describe
        /// the same physical distance -- almost always a mis-picked point or a
        /// unit mix-up -- and the caller should stop rather than place a
        /// stretched layout that no rotation can fix.
        /// </summary>
        public static double MeasureScaleError(UV sourceA, UV sourceB, XYZ targetA, XYZ targetB)
        {
            double sourceLength = Math.Sqrt(
                Math.Pow(LengthUnits.MillimetresToFeet(sourceB.U - sourceA.U), 2) +
                Math.Pow(LengthUnits.MillimetresToFeet(sourceB.V - sourceA.V), 2));

            double targetLength = Math.Sqrt(
                Math.Pow(targetB.X - targetA.X, 2) + Math.Pow(targetB.Y - targetA.Y, 2));

            if (sourceLength <= 1e-9)
            {
                return double.NaN;
            }

            return targetLength / sourceLength;
        }

        /// <summary>Transforms a DIALux point. X and Y are millimetres; Z is already in feet.</summary>
        public XYZ ToRevit(double xMillimetres, double yMillimetres, double zFeet)
        {
            double x = LengthUnits.MillimetresToFeet(xMillimetres) - _originX;
            double y = LengthUnits.MillimetresToFeet(yMillimetres) - _originY;

            return new XYZ(
                (x * _cos) - (y * _sin) + _offsetX,
                (x * _sin) + (y * _cos) + _offsetY,
                zFeet);
        }
    }
}
