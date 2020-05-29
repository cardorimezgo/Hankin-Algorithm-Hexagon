using System;
using System.Collections;
using System.Collections.Generic;

using Rhino;
using Rhino.Geometry;

using Grasshopper;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

using Rhino.Collections;
using System.Linq;

/// <summary>
/// This class will be instantiated on demand by the Script component.
/// </summary>
public class Script_Instance : GH_ScriptInstance
{
    #region Utility functions
    /// <summary>Print a String to the [Out] Parameter of the Script component.</summary>
    /// <param name="text">String to print.</param>
    private void Print(string text) { /* Implementation hidden. */ }
    /// <summary>Print a formatted String to the [Out] Parameter of the Script component.</summary>
    /// <param name="format">String format.</param>
    /// <param name="args">Formatting parameters.</param>
    private void Print(string format, params object[] args) { /* Implementation hidden. */ }
    /// <summary>Print useful information about an object instance to the [Out] Parameter of the Script component. </summary>
    /// <param name="obj">Object instance to parse.</param>
    private void Reflect(object obj) { /* Implementation hidden. */ }
    /// <summary>Print the signatures of all the overloads of a specific method to the [Out] Parameter of the Script component. </summary>
    /// <param name="obj">Object instance to parse.</param>
    private void Reflect(object obj, string method_name) { /* Implementation hidden. */ }
    #endregion

    #region Members
    /// <summary>Gets the current Rhino document.</summary>
    private readonly RhinoDoc RhinoDocument;
    /// <summary>Gets the Grasshopper document that owns this script.</summary>
    private readonly GH_Document GrasshopperDocument;
    /// <summary>Gets the Grasshopper script component that owns this script.</summary>
    private readonly IGH_Component Component;
    /// <summary>
    /// Gets the current iteration count. The first call to RunScript() is associated with Iteration==0.
    /// Any subsequent call within the same solution will increment the Iteration count.
    /// </summary>
    private readonly int Iteration;
    #endregion

    /// <summary>
    /// This procedure contains the user code. Input parameters are provided as regular arguments,
    /// Output parameters as ref arguments. You don't have to assign output parameters,
    /// they will have a default value.
    /// </summary>
    private void RunScript(double size, double cenX, double cenY, double delta, double theta, double DEF, ref object A)
    {
        Tiling th;
        th = new Tiling();

        /////////// -SLIDERS MAX & MIN LIMITS - ///////////
        double DeltaMAX = size * 0.5;
        double ThetaMAX = 75;
        double DEFMAX = 0.1;

        var inputDelta = Component.Params.Input[3].Sources[0]; //get the first thing connected to the first input of this component
        var sliderD = inputDelta as Grasshopper.Kernel.Special.GH_NumberSlider; //try to cast that thing as a slider

        if (sliderD != null) //if the component was successfully cast as a slider
        {
            sliderD.Slider.Minimum = 0;
            sliderD.Slider.Maximum = (decimal)DeltaMAX;
        }

        var inputTheta = Component.Params.Input[4].Sources[0];
        var sliderT = inputTheta as Grasshopper.Kernel.Special.GH_NumberSlider;

        if (sliderT != null)
        {
            sliderT.Slider.Minimum = 0;
            sliderT.Slider.Maximum = (decimal)ThetaMAX;
        }

        var inputDEF = Component.Params.Input[5].Sources[0];
        var sliderDEF = inputDEF as Grasshopper.Kernel.Special.GH_NumberSlider;

        if (sliderDEF != null)
        {
            sliderDEF.Slider.Minimum = 0;
            sliderDEF.Slider.Maximum = (decimal)DEFMAX;
        }

        /////////////// -RENDERING- ///////////////////

        A = th.TilingHex(size, cenX, cenY, delta, theta, DEF);

    }

    // <Custom additional code> 

    public class Tiling
    {
        List<Hexagon> hex;

        public Tiling()
        {
            hex = new List<Hexagon>();
        }

        public List<LineCurve> TilingHex(double _size, double _xNumCell, double _yNumCell, double _delta, double _theta, double _DEF)  ////// MATRIX CREATION
        {
            double h = 2 * _size;
            double w = Math.Sqrt(3) * _size;
            double vertCent = h * 0.75;
            double matX = w * _xNumCell;
            double matY = h * _yNumCell;
            int row = 0;
            double plus = 0;
            List<LineCurve> lc = new List<LineCurve>();

            for (double cenY = h; cenY <= matY; cenY += vertCent)
            {
                double offX = ((row % 2) == 0) ? w : w / 2;

                for (double cenX = offX; cenX <= matX; cenX += w)
                {
                    hex.Add(new Hexagon(_size, cenX, cenY, _delta, _theta + plus)); ////EACH INSTANCE OF AN HEXAGON IN THE MATRIX
                    plus += _DEF; /// PARQUETE DEFORMATION
                }
                row++;
            }
            for (int i = 0; i < hex.Count; i++)
            {
                lc.AddRange(hex[i].HexLns());
            }
            return lc;
        }
    }

    /////////////// --HANKIN ALGORITHM-- //////////////////////
    ///// "IF" POLIGONO REGULAR
    //// GENERAR UN "IF" EN CASO DE QUE EL POLIGONO TENGA MAS--
    /// -- DE "X" # DE LADOS PARA AGREGAR HANKIN CADA TERCER LADO
    //

    class Hankin
    {
        int sides;
        Point3d[] pts;
        double delta;
        double theta;

        public Hankin(Point3d[] _pts, int _sides, double _delta, double _theta)
        {
            this.pts = _pts;
            this.delta = _delta;
            this.theta = _theta;
            this.sides = _sides;
        }

        public Tuple<List<LineCurve>, List<LineCurve>> Lines()
        {
            Vector3d midH = new Vector3d();
            List<LineCurve> h1 = new List<LineCurve>(sides);
            List<LineCurve> h2 = new List<LineCurve>(sides);

            for (int i = 0; i < pts.Length - 1; i++)
            {
                midH = new Vector3d(pts[i] + pts[i + 1]);
                midH = Vector3d.Multiply(midH, 0.5);
                Vector3d m1 = new Vector3d(pts[i] - midH);
                Vector3d m2 = new Vector3d(pts[i + 1] - midH);

                double baseLen = m1.Length + delta;
                double intAng = (sides - 2) * (Math.PI / sides);
                double alpha = intAng * 0.5;
                double beta = Math.PI - alpha - RhinoMath.ToRadians(theta);
                double len = Math.Sin(alpha) * ((baseLen) / Math.Sin(beta));

                Vector3d offset1 = midH;
                Vector3d offset2 = midH;

                if (delta > 0)
                {
                    m1.Unitize();
                    m2.Unitize();
                    m1 = Vector3d.Multiply(m1, delta);
                    m2 = Vector3d.Multiply(m2, delta);
                    offset1 = Vector3d.Add(m2, midH);
                    offset2 = Vector3d.Add(m1, midH);
                }

                m1.Unitize();
                m2.Unitize();

                m1.Rotate(RhinoMath.ToRadians(-theta), Vector3d.ZAxis);
                m2.Rotate(RhinoMath.ToRadians(theta), Vector3d.ZAxis);

                m1.Unitize();
                m2.Unitize();
                m1 = Vector3d.Multiply(m1, len);
                m2 = Vector3d.Multiply(m2, len);

                Vector3d hlen = Vector3d.Add(offset1, m1);
                Vector3d hlen2 = Vector3d.Add(offset2, m2);

                h1.Add(new LineCurve(new Point3d(offset1), new Point3d(hlen)));
                h2.Add(new LineCurve(new Point3d(offset2), new Point3d(hlen2)));
            }
            return Tuple.Create(h1, h2);
        }

        public List<LineCurve> Ends()
        {
            List<LineCurve> lns = new List<LineCurve>(2);
            Vector3d midH = new Vector3d();
            midH = new Vector3d(pts[sides - 1] + pts[0]);
            midH = Vector3d.Multiply(midH, 0.5);
            Vector3d m1 = new Vector3d(pts[sides - 1] - midH);
            Vector3d m2 = new Vector3d(pts[0] - midH);

            double baseLen = m1.Length + delta;
            double intAng = (sides - 2) * (Math.PI / sides);
            double alpha = intAng * 0.5;
            double beta = Math.PI - alpha - RhinoMath.ToRadians(theta);
            double len = Math.Sin(alpha) * ((baseLen) / Math.Sin(beta));

            Vector3d offset1 = midH;
            Vector3d offset2 = midH;

            if (delta > 0)
            {
                m1.Unitize();
                m2.Unitize();
                m1 = Vector3d.Multiply(m1, delta);
                m2 = Vector3d.Multiply(m2, delta);
                offset1 = Vector3d.Add(m2, midH);
                offset2 = Vector3d.Add(m1, midH);
            }

            m1.Unitize();
            m2.Unitize();

            m1.Rotate(RhinoMath.ToRadians(-theta), Vector3d.ZAxis);
            m2.Rotate(RhinoMath.ToRadians(theta), Vector3d.ZAxis);

            m1.Unitize();
            m2.Unitize();
            m1 = Vector3d.Multiply(m1, len);
            m2 = Vector3d.Multiply(m2, len);

            Vector3d hlen = Vector3d.Add(offset1, m1);
            Vector3d hlen2 = Vector3d.Add(offset2, m2);

            LineCurve h1 = (new LineCurve(new Point3d(offset1), new Point3d(hlen)));
            LineCurve h2 = (new LineCurve(new Point3d(offset2), new Point3d(hlen2)));
            lns.Add(h1);
            lns.Add(h2);
            return lns;
        }
    }

    ///////////// --Hexagon Cell Creation-- /////////////
    /////////////
    //////

    class Hexagon
    {
        double cenX;
        double cenY;
        double size;
        double delta;
        double theta;
        int sides;

        public Hexagon(double _size, double _cenX, double _cenY, double _delta, double _theta)
        {
            this.cenX = _cenX;
            this.cenY = _cenY;
            this.size = _size;
            this.delta = _delta;
            this.theta = _theta;
            sides = 6;
        }

        public Point3d[] HexPts()
        {
            Point3d[] pts = new Point3d[sides];

            for (int i = 0; i < sides; i++)
            {
                double angle_deg = 60 * i - 30;
                double angle_rad = Math.PI / 180 * angle_deg;
                double edX = cenX + size * Math.Cos(angle_rad);
                double edY = cenY + size * Math.Sin(angle_rad);
                pts[i] = new Point3d(edX, edY, 0);
            }
            return pts;
        }

        public List<LineCurve> HexLns()
        {
            Hankin H;
            H = new Hankin(HexPts(), sides, delta, theta);
            List<LineCurve> lns = new List<LineCurve>(sides);
            List<LineCurve> lnsI1 = new List<LineCurve>(sides);
            List<LineCurve> lnsI2 = new List<LineCurve>(sides);
            List<LineCurve> lnsI3 = new List<LineCurve>(sides);

            for (int j = 0; j < sides - 1; j++)
            {
                lns.Add(new LineCurve(HexPts()[j], HexPts()[j + 1]));
            }
            lns.Add(new LineCurve(HexPts()[sides - 1], HexPts()[0]));
            lnsI1 = H.Lines().Item1;
            lnsI2 = H.Lines().Item2;
            lnsI3 = H.Ends();
            var Lines = lns.Concat(lnsI1).Concat(lnsI2).Concat(lnsI3).ToList();

            return Lines;
        }
    }
    // </Custom additional code> 
}