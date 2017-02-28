using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows.Forms;

using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using Tekla.Structures.Plugins;
using Tekla.Structures;

namespace ZL_ObliqueWasher_pl
{
    public class PluginData
    {
        #region Fields
        [StructuresField("name")]
        public string name;
        [StructuresField("material")]
        public string material;
        [StructuresField("finish")]
        public string finish;
        [StructuresField("prefix_part")]
        public string prefix_part;
        [StructuresField("prefix_asm")]
        public string prefix_asm;
        [StructuresField("start_part")]
        public int start_part;
        [StructuresField("start_asm")]
        public int start_asm;
        #endregion
    }


    [Plugin("ZL_ObliqueWasher_pl")]
    [PluginUserInterface("ZL_ObliqueWasher_pl.MainForm")]
    public class ZL_ObliqueWasher_pl : PluginBase
    {
        #region Fields
        private Model _Model;
        private PluginData _Data;

        private string name = "Шайба";
        private string material = "STEEL";
        private string finish = "";
        private string prefix_part = "";
        private string prefix_asm = "МД";
        private int start_part = 1;
        private int start_asm = 1;


        Dictionary<double, List<double>> GOST_10906_78 = new Dictionary<double, List<double>>();
        #endregion

        #region Properties
        private Model Model
        {
            get { return this._Model; }
            set { this._Model = value; }
        }

        private PluginData Data
        {
            get { return this._Data; }
            set { this._Data = value; }
        }
        #endregion

        #region Constructor
        public ZL_ObliqueWasher_pl(PluginData data)
        {
            Model = new Model();
            Data = data;
        }
        #endregion

        #region Overrides
        public override List<InputDefinition> DefineInput()
        {
            //
            // This is an example for selecting two points; change this to suit your needs.
            //
            List<InputDefinition> PointList = new List<InputDefinition>();
            Picker Picker = new Picker();
            ModelObject PickedPoints = Picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_BOLTGROUP);

            PointList.Add(new InputDefinition(PickedPoints.Identifier));

            return PointList;
        }

        public override bool Run(List<InputDefinition> Input)
        {
            try
            {
                GetValuesFromDialog();

                WorkPlaneHandler wph = Model.GetWorkPlaneHandler();

                TransformationPlane tp = wph.GetCurrentTransformationPlane();
                TransformationPlane tppart = null;


                BoltGroup bg = Model.SelectModelObject((Identifier)Input[0].GetInput()) as BoltGroup;
                        bg.Select();
                        List<Part> parts = new List<Part>();
                        parts.Add(bg.PartToBeBolted);
                        parts.Add(bg.PartToBoltTo);
                        foreach (Part p in bg.OtherPartsToBolt)
                        {
                            parts.Add(p);
                        }

                        #region Чистим дубликаты
                        List<Part> _part = new List<Part>();

                        foreach (Part p in parts)
                        {
                            bool flag = false;
                            foreach (Part pp in _part)
                            {
                                if (pp.Identifier.ID == p.Identifier.ID) flag = true;
                            }
                            if (!flag) _part.Add(p);
                        }

                        parts.Clear();
                        parts = _part;
                        #endregion

                        foreach (Part p in parts)
                        {
                            if (p is Beam)
                            {
                                Beam b = p as Beam;
                                b.Select();

                                double k = 0.0; b.GetReportProperty("PROFILE.FLANGE_SLOPE_RATIO", ref k);
                                if (k == 0) continue;

                                tppart = new TransformationPlane(p.GetCoordinateSystem());
                                wph.SetCurrentTransformationPlane(tppart);
                                bg.Select();
                                foreach (Point pb in bg.BoltPositions)
                                {

                                    Point _pb = new Point(pb);

                                    #region Уклон полок - точки через солид

                                    GeometricPlane gp = new GeometricPlane(
                                        _pb,
                                        new Vector(0, 1, 0),
                                        new Vector(0, 0, 1));
                                    List<List<Point>> lp = IntersectSolid(p.GetSolid(), gp);

                                    List<LineSegment> ls = new List<LineSegment>();


                                    for (int i = 0; i < lp[0].Count - 1; i++)
                                    {
                                        Point p1 = lp[0][i];
                                        Point p2 = lp[0][i + 1];
                                        Vector v = new Vector(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
                                        v.Normalize(1.0);
                                        if (v.Y != 0 && v.Z != 0)
                                        {
                                            ControlLine cl = new ControlLine();
                                            cl.Line.Point1 = p1;
                                            cl.Line.Point2 = p2;
                                            // cl.Insert();
                                            ls.Add(new LineSegment(p1, p2));

                                        }
                                    }

                                    Point _p1 = lp[0][0];
                                    Point _p2 = lp[0][lp[0].Count - 1];
                                    Vector _v = new Vector(_p2.X - _p1.X, _p2.Y - _p1.Y, _p2.Z - _p1.Z);
                                    if (_v.Y != 0 && _v.Z != 0)
                                    {
                                        ls.Add(new LineSegment(_p1, _p2));
                                        ControlLine cl = new ControlLine();
                                        cl.Line.Point1 = _p1;
                                        cl.Line.Point2 = _p2;
                                        // cl.Insert();
                                    }


                                    #endregion

                                    #region Точки для построения пластины

                                    double diam = bg.BoltSize;

                                    double tol = GOST_10906_78[diam][0];
                                    double _b = GOST_10906_78[diam][1];
                                    double t1 = GOST_10906_78[diam][2];
                                    double t2 = GOST_10906_78[diam][3];

                                    int kf = (_pb.Z <= ((Point)b.GetReferenceLine(false)[0]).Z ? -1 : 1);

                                    _pb.Z += kf * _b * 0.5;


                                    double h = double.MaxValue;
                                    LineSegment lsb = ls[0];

                                    foreach (LineSegment lsi in ls)
                                    {
                                        double t = Distance.PointToLineSegment(_pb, lsi);
                                        if (h >= t)
                                        {
                                            h = t;
                                            lsb = lsi;
                                        }
                                    }
                                    //ControlLine cli = new ControlLine();
                                    //cli.Line.Point1 = lsb.Point1;
                                    //cli.Line.Point2 = lsb.Point2;
                                    //cli.Insert();
                                    Point pb1 = new Point(_pb.X, _pb.Y + 1000, _pb.Z);

                                    Point pbi = Intersection.LineToLine(
                                        new Line(lsb),
                                        new Line(_pb, pb1)).Point1;
                                    //cli.Line.Point1 = _pb;
                                    //cli.Line.Point2 = pbi;
                                    //cli.Insert();

                                    #endregion

                                    ContourPlate cp = new ContourPlate();

                                    Contour cr = new Contour();
                                    cr.AddContourPoint(new ContourPoint(new Point(pbi.X - _b * 0.5, pbi.Y, pbi.Z), null));

                                    cr.AddContourPoint(new ContourPoint(new Point(pbi.X + _b * 0.5, pbi.Y, pbi.Z), null));
                                    cr.AddContourPoint(new ContourPoint(new Point(pbi.X + _b * 0.5, pbi.Y, pbi.Z - kf * _b), null));
                                    cr.AddContourPoint(new ContourPoint(new Point(pbi.X - _b * 0.5, pbi.Y, pbi.Z - kf * _b), null));

                                    cp.Contour = cr;


                                    cp.Profile.ProfileString = "PL" + t1.ToString();
                                    cp.AssemblyNumber.Prefix = prefix_asm;
                                    cp.AssemblyNumber.StartNumber = start_part;
                                    cp.PartNumber.Prefix = prefix_part;
                                    cp.PartNumber.StartNumber = start_part;

                                    cp.Name = name;
                                    cp.Material.MaterialString = material;
                                    cp.Finish = finish;

                                    if (kf == -1 && pbi.Y > 0)
                                        cp.Position.Depth = Position.DepthEnum.FRONT;
                                    else if (kf == -1 && pbi.Y < 0)
                                        cp.Position.Depth = Position.DepthEnum.BEHIND;
                                    else if (kf == 1 && pbi.Y > 0)
                                        cp.Position.Depth = Position.DepthEnum.BEHIND;
                                    else if (kf == 1 && pbi.Y < 0)
                                        cp.Position.Depth = Position.DepthEnum.FRONT;

                                    cp.Insert();


                                    BooleanPart bp = new BooleanPart();
                                    ContourPlate cp2 = new ContourPlate();
                                    Contour cr2 = new Contour();

                                    cr2.AddContourPoint(new ContourPoint(new Point(pbi.X, pbi.Y, pbi.Z), null));
                                    cr2.AddContourPoint(new ContourPoint(new Point(pbi.X, pbi.Y, pbi.Z - kf * _b), null));
                                    cr2.AddContourPoint(new ContourPoint(new Point(pbi.X, pbi.Y + (pbi.Y > 0 ? -1 * (t1 - t2) : (t1 - t2)), pbi.Z - kf * _b), null));

                                    cp2.Contour = cr2;
                                    cp2.Profile.ProfileString = "PL" + (_b + 10).ToString();
                                    cp2.Class = BooleanPart.BooleanOperativeClassName;

                                    cp2.Insert();

                                    bp.Father = cp;
                                    bp.OperativePart = cp2;
                                    bp.Insert();
                                    cp2.Delete();

                                    BoltArray ba = new BoltArray();
                                    ba.FirstPosition = pb;
                                    ba.SecondPosition = new Point(pb.X + 100, pb.Y, pb.Z);

                                    ba.BoltStandard = bg.BoltStandard;
                                    ba.Position.Rotation = Position.RotationEnum.TOP;
                                    ba.BoltType = bg.BoltType;
                                    ba.BoltSize = bg.BoltSize;
                                    ba.Tolerance = tol;
                                    ba.Bolt = false;
                                    ba.AddBoltDistX(0);
                                    ba.AddBoltDistY(0);
                                    ba.PartToBeBolted = cp;
                                    ba.PartToBoltTo = cp;
                                    ba.Insert();
                                }

                            }

                        }

                wph.SetCurrentTransformationPlane(tp);
            }
            catch (Exception Exc)
            {
                MessageBox.Show(Exc.ToString());
            }

            return true;
        }
        #endregion

        #region Private methods
        /// <summary>
        /// Gets the values from the dialog and sets the default values if needed
        /// </summary>
        void GetValuesFromDialog()
        {
            GOST_10906_78.Add(6, new List<double>() { 0.6, 16, 5.8, 4.9 });
            GOST_10906_78.Add(8, new List<double>() { 1, 16, 5.8, 4.9 });
            GOST_10906_78.Add(10, new List<double>() { 1, 20, 6.2, 4.9 });
            GOST_10906_78.Add(12, new List<double>() { 1, 30, 7.3, 5.7 });
            GOST_10906_78.Add(14, new List<double>() { 1, 30, 7.3, 5.7 });
            GOST_10906_78.Add(16, new List<double>() { 1, 30, 7.3, 5.7 });
            GOST_10906_78.Add(18, new List<double>() { 1, 40, 8.4, 6.2 });
            GOST_10906_78.Add(20, new List<double>() { 2, 40, 8.4, 6.2 });
            GOST_10906_78.Add(22, new List<double>() { 2, 40, 8.4, 6.2 });
            GOST_10906_78.Add(24, new List<double>() { 2, 50, 9.5, 6.8 });
            GOST_10906_78.Add(27, new List<double>() { 3, 50, 9.5, 6.8 });


            if (!IsDefaultValue(Data.finish)) finish = Data.finish;
            if (!IsDefaultValue(Data.material)) material = Data.material;
            if (!IsDefaultValue(Data.name)) name = Data.name;
            if (!IsDefaultValue(Data.prefix_asm)) prefix_asm = Data.prefix_asm;
            if (!IsDefaultValue(Data.prefix_part)) prefix_part = Data.prefix_part;
            if (!IsDefaultValue(Data.start_asm)) start_asm = Data.start_asm;
            if (!IsDefaultValue(Data.start_part)) start_part = Data.start_part;
        }

        #region Пересечение солида геометрической плоскостью
        /// <summary>
        /// Пересечение солида геометрической плоскостью
        /// </summary>
        /// <param name="solid">Солид</param>
        /// <param name="plane">Геометрическая плоскость</param>
        /// <returns>Коллекция полигонов(коллекций точек)</returns>
        List<List<Point>> IntersectSolid(Solid solid, GeometricPlane plane)
        {

            Point p1, p2, p3 = null;
            p1 = new Point(plane.Origin);
            p2 = new Point(p1);
            p2.Translate(100, 100, 100);
            p2 = Projection.PointToPlane(p2, plane);
            Vector x = new Vector(p2 - p1);
            x.Normalize(1000);
            Vector y = Vector.Cross(plane.Normal, x);
            y.Normalize(1000);
            p3 = new Point(p1 + y);
            p3 = Projection.PointToPlane(p3, plane);

            IEnumerator faceEnumerator = solid.IntersectAllFaces(p1, p2, p3);
            List<List<Point>> polygons = new List<List<Point>>();
            while (faceEnumerator.MoveNext())
            {
                ArrayList points = faceEnumerator.Current as ArrayList;
                IEnumerator LoopsEnum = points.GetEnumerator();
                while (LoopsEnum.MoveNext())
                {
                    ArrayList ps = LoopsEnum.Current as ArrayList;
                    List<Point> polygon = new List<Point>();
                    foreach (object o in ps)
                    {
                        Point p = o as Point;
                        polygon.Add(p);
                    }
                    polygons.Add(polygon);
                }

            }
            return polygons;
        }
        #endregion
        #endregion
    }
}
