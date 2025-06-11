using System;
using System.Collections.Generic;
using System.Linq;
using ScottPlot;
using ScottPlot.Rendering.RenderActions;

namespace IsolinePlotting;

// Type aliases and delegates
public delegate double Func(double[] point);

// Point representation
public struct Point
{
    public double[] Coordinates { get; }

    public Point(params double[] coordinates)
    {
        Coordinates = coordinates;
    }

    public Point(double x, double y) : this(new[] { x, y }) { }

    public double this[int index] => Coordinates[index];

    public static Point operator +(Point a, Point b)
    {
        var result = new double[a.Coordinates.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = a[i] + b[i];
        return new Point(result);
    }

    public static Point operator -(Point a, Point b)
    {
        var result = new double[a.Coordinates.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = a[i] - b[i];
        return new Point(result);
    }

    public static Point operator *(Point p, double scalar)
    {
        var result = new double[p.Coordinates.Length];
        for (int i = 0; i < result.Length; i++)
            result[i] = p[i] * scalar;
        return new Point(result);
    }

    public static Point operator /(Point p, double scalar)
    {
        return p * (1.0 / scalar);
    }
}

// ValuedPoint class
public class ValuedPoint
{
    public Point Pos { get; set; }
    public double Val { get; set; }

    public ValuedPoint(Point pos, double? val = null)
    {
        Pos = pos;
        Val = val ?? double.NaN;
    }

    public ValuedPoint Calc(Func fn)
    {
        Val = fn(Pos.Coordinates);
        return this;
    }

    public static ValuedPoint Midpoint(ValuedPoint p1, ValuedPoint p2, Func fn)
    {
        var mid = (p1.Pos + p2.Pos) / 2;
        return new ValuedPoint(mid, fn(mid.Coordinates));
    }

    public static ValuedPoint IntersectZero(ValuedPoint p1, ValuedPoint p2, Func fn)
    {
        double denom = p1.Val - p2.Val;
        double k1 = -p2.Val / denom;
        double k2 = p1.Val / denom;
        var pt = p1.Pos * k1 + p2.Pos * k2;
        return new ValuedPoint(pt, fn(pt.Coordinates));
    }

    public override string ToString()
    {
        return $"({Pos[0]},{Pos[1]}; {Val})";
    }
}

// Triangle class
public class Triangle
{
    public List<ValuedPoint> Vertices { get; }
    public Triangle? Next { get; set; }
    public ValuedPoint? NextBisectPoint { get; set; }
    public Triangle? Prev { get; set; }
    public bool Visited { get; set; }

    public Triangle(List<ValuedPoint> vertices)
    {
        Vertices = vertices;
    }
}

// MinimalCell class
public class MinimalCell
{
    public int Dim { get; }
    public List<ValuedPoint> Vertices { get; }

    public MinimalCell(int dim, List<ValuedPoint> vertices)
    {
        Dim = dim;
        Vertices = vertices;
    }

    public MinimalCell GetSubcell(int axis, int dir)
    {
        int m = 1 << axis;
        var subVertices = new List<ValuedPoint>();
        for (int i = 0; i < Vertices.Count; i++)
        {
            if (((i & m) > 0) == (dir == 1))
                subVertices.Add(Vertices[i]);
        }
        return new MinimalCell(Dim - 1, subVertices);
    }

    public ValuedPoint GetDual(Func fn)
    {
        return ValuedPoint.Midpoint(Vertices[0], Vertices[Vertices.Count - 1], fn);
    }
}

// Cell class
public class Cell : MinimalCell
{

    public int Depth { get; }
    public List<Cell> Children { get; }
    public Cell? Parent { get; }
    public int ChildDirection { get; }

    public Cell(int dim, List<ValuedPoint> vertices, int depth, List<Cell> children, Cell? parent, int childDirection)
        : base(dim, vertices)
    {
        Depth = depth;
        Children = children ?? new List<Cell>();
        Parent = parent;
        ChildDirection = childDirection;
    }

    public void ComputeChildren(Func fn)
    {
        if (Children.Count > 0) throw new InvalidOperationException("Children already computed");

        for (int i = 0; i < Vertices.Count; i++)
        {
            var pmin = (Vertices[0].Pos + Vertices[i].Pos) / 2;
            var pmax = (Vertices[Vertices.Count - 1].Pos + Vertices[i].Pos) / 2;
            var vertices = VerticesFromExtremes(Dim, pmin, pmax, fn);
            var newQuad = new Cell(Dim, vertices, Depth + 1, new List<Cell>(), this, i);
            Children.Add(newQuad);
        }
    }

    public IEnumerable<Cell> GetLeavesInDirection(int axis, int dir)
    {
        if (Children.Count > 0)
        {
            int m = 1 << axis;
            for (int i = 0; i < (1 << Dim); i++)
            {
                if (((i & m) > 0) == (dir == 1))
                {
                    foreach (var leaf in Children[i].GetLeavesInDirection(axis, dir))
                        yield return leaf;
                }
            }
        }
        else
        {
            yield return this;
        }
    }

    public Cell? WalkInDirection(int axis, int dir)
    {
        int m = 1 << axis;
        if (((ChildDirection & m) > 0) == (dir == 1))
        {
            if (Parent == null) return null;
            var parentWalked = Parent.WalkInDirection(axis, dir);
            if (parentWalked != null && parentWalked.Children.Count > 0)
                return parentWalked.Children[ChildDirection ^ m];
            else
                return parentWalked;
        }
        else
        {
            if (Parent == null) return null;
            return Parent.Children[ChildDirection ^ m];
        }
    }

    public IEnumerable<Cell?> WalkLeavesInDirection(int axis, int dir)
    {
        var walked = WalkInDirection(axis, dir);
        if (walked != null)
        {
            foreach (var leaf in walked.GetLeavesInDirection(axis, 1 - dir))
                yield return leaf;
        }
        else
        {
            yield return null;
        }
    }

    private static List<ValuedPoint> VerticesFromExtremes(int dim, Point pmin, Point pmax, Func fn)
    {
        var w = pmax - pmin;
        var vertices = new List<ValuedPoint>();

        for (int i = 0; i < (1 << dim); i++)
        {
            var coords = new double[dim];
            for (int d = 0; d < dim; d++)
            {
                coords[d] = pmin[d] + ((i >> d) & 1) * w[d];
            }
            vertices.Add(new ValuedPoint(new Point(coords)).Calc(fn));
        }

        return vertices;
    }
}

// Triangulator class
public class Triangulator
{
    private List<Triangle> triangles = new List<Triangle>();
    private Dictionary<string, Triangle> hangingNext = new Dictionary<string, Triangle>();
    private Cell root;
    private Func fn;
    private double[] tol;

    public Triangulator(Cell root, Func fn, double[] tol)
    {
        this.root = root;
        this.fn = fn;
        this.tol = tol;
    }

    public List<Triangle> Triangulate()
    {
        TriangulateInside(root);
        return triangles;
    }

    private void TriangulateInside(Cell quad)
    {
        if (quad.Children.Count > 0)
        {
            foreach (var child in quad.Children)
                TriangulateInside(child);

            TriangulateCrossingRow(quad.Children[0], quad.Children[1]);
            TriangulateCrossingRow(quad.Children[2], quad.Children[3]);
            TriangulateCrossingCol(quad.Children[0], quad.Children[2]);
            TriangulateCrossingCol(quad.Children[1], quad.Children[3]);
        }
    }

    private void TriangulateCrossingRow(Cell a, Cell b)
    {
        if (a.Children.Count > 0 && b.Children.Count > 0)
        {
            TriangulateCrossingRow(a.Children[1], b.Children[0]);
            TriangulateCrossingRow(a.Children[3], b.Children[2]);
        }
        else if (a.Children.Count > 0)
        {
            TriangulateCrossingRow(a.Children[1], b);
            TriangulateCrossingRow(a.Children[3], b);
        }
        else if (b.Children.Count > 0)
        {
            TriangulateCrossingRow(a, b.Children[0]);
            TriangulateCrossingRow(a, b.Children[2]);
        }
        else
        {
            var faceDualA = GetFaceDual(a);
            var faceDualB = GetFaceDual(b);
            Tuple<Triangle, Triangle, Triangle, Triangle> triangles;

            if (a.Depth < b.Depth)
            {
                var edgeDual = GetEdgeDual(b.Vertices[2], b.Vertices[0]);
                triangles = FourTriangles(b.Vertices[2], faceDualB, b.Vertices[0], faceDualA, edgeDual);
            }
            else
            {
                var edgeDual = GetEdgeDual(a.Vertices[3], a.Vertices[1]);
                triangles = FourTriangles(a.Vertices[3], faceDualB, a.Vertices[1], faceDualA, edgeDual);
            }

            AddFourTriangles(triangles);
        }
    }

    private void TriangulateCrossingCol(Cell a, Cell b)
    {
        if (a.Children.Count > 0 && b.Children.Count > 0)
        {
            TriangulateCrossingCol(a.Children[2], b.Children[0]);
            TriangulateCrossingCol(a.Children[3], b.Children[1]);
        }
        else if (a.Children.Count > 0)
        {
            TriangulateCrossingCol(a.Children[2], b);
            TriangulateCrossingCol(a.Children[3], b);
        }
        else if (b.Children.Count > 0)
        {
            TriangulateCrossingCol(a, b.Children[0]);
            TriangulateCrossingCol(a, b.Children[1]);
        }
        else
        {
            var faceDualA = GetFaceDual(a);
            var faceDualB = GetFaceDual(b);
            Tuple<Triangle, Triangle, Triangle, Triangle> triangles;

            if (a.Depth < b.Depth)
            {
                var edgeDual = GetEdgeDual(b.Vertices[0], b.Vertices[1]);
                triangles = FourTriangles(b.Vertices[0], faceDualB, b.Vertices[1], faceDualA, edgeDual);
            }
            else
            {
                var edgeDual = GetEdgeDual(a.Vertices[2], a.Vertices[3]);
                triangles = FourTriangles(a.Vertices[2], faceDualB, a.Vertices[3], faceDualA, edgeDual);
            }

            AddFourTriangles(triangles);
        }
    }

    private void AddFourTriangles(Tuple<Triangle, Triangle, Triangle, Triangle> triangles)
    {
        for (int i = 0; i < 4; i++)
        {
            var tri1 = GetTriangleByIndex(triangles, i);
            var tri2 = GetTriangleByIndex(triangles, (i + 1) % 4);
            var tri3 = GetTriangleByIndex(triangles, (i + 2) % 4);
            NextSandwichTriangles(tri1, tri2, tri3);
        }

        this.triangles.Add(triangles.Item1);
        this.triangles.Add(triangles.Item2);
        this.triangles.Add(triangles.Item3);
        this.triangles.Add(triangles.Item4);
    }

    private Triangle GetTriangleByIndex(Tuple<Triangle, Triangle, Triangle, Triangle> triangles, int index)
    {
        switch (index)
        {
            case 0: return triangles.Item1;
            case 1: return triangles.Item2;
            case 2: return triangles.Item3;
            case 3: return triangles.Item4;
            default: throw new ArgumentOutOfRangeException();
        }
    }

    private void SetNext(Triangle tri1, Triangle tri2, ValuedPoint vpos, ValuedPoint vneg)
    {
        if (!(vpos.Val > 0 && vneg.Val <= 0)) return;

        var (intersection, isZero) = IsolinePlotter.BinarySearchZero(vpos, vneg, fn, tol);
        if (!isZero) return;

        tri1.NextBisectPoint = intersection;
        tri1.Next = tri2;
        tri2.Prev = tri1;
    }

    private void NextSandwichTriangles(Triangle a, Triangle b, Triangle c)
    {
        var center = b.Vertices[2];
        var x = b.Vertices[0];
        var y = b.Vertices[1];

        // Simple connections
        if (center.Val > 0 && y.Val <= 0)
            SetNext(b, c, center, y);

        if (x.Val > 0 && center.Val <= 0)
            SetNext(b, a, x, center);

        // Hanging connections
        var id = GetEdgeId(x.Pos, y.Pos);

        if (y.Val > 0 && x.Val <= 0)
        {
            if (hangingNext.ContainsKey(id))
            {
                SetNext(b, hangingNext[id], y, x);
                hangingNext.Remove(id);
            }
            else
            {
                hangingNext[id] = b;
            }
        }
        else if (y.Val <= 0 && x.Val > 0)
        {
            if (hangingNext.ContainsKey(id))
            {
                SetNext(hangingNext[id], b, x, y);
                hangingNext.Remove(id);
            }
            else
            {
                hangingNext[id] = b;
            }
        }
    }

    private string GetEdgeId(Point p1, Point p2)
    {
        var mid = (p1 + p2) / 2;
        return string.Join(",", mid.Coordinates.Select(c => c.ToString("F10")));
    }

    private ValuedPoint GetEdgeDual(ValuedPoint p1, ValuedPoint p2)
    {
        if ((p1.Val > 0) != (p2.Val > 0))
        {
            return ValuedPoint.Midpoint(p1, p2, fn);
        }

        double dt = 0.01;
        var pos1 = p1.Pos * (1 - dt) + p2.Pos * dt;
        var pos2 = p1.Pos * dt + p2.Pos * (1 - dt);

        double df1 = fn(pos1.Coordinates);
        double df2 = fn(pos2.Coordinates);

        if ((df1 > 0) == (df2 > 0))
        {
            return ValuedPoint.Midpoint(p1, p2, fn);
        }
        else
        {
            var v1 = new ValuedPoint(p1.Pos, df1);
            var v2 = new ValuedPoint(p2.Pos, df2);
            return ValuedPoint.IntersectZero(v1, v2, fn);
        }
    }

    private ValuedPoint GetFaceDual(Cell quad)
    {
        return ValuedPoint.Midpoint(quad.Vertices[0], quad.Vertices[quad.Vertices.Count - 1], fn);
    }

    private static Tuple<Triangle, Triangle, Triangle, Triangle> FourTriangles(
        ValuedPoint a, ValuedPoint b, ValuedPoint c, ValuedPoint d, ValuedPoint center)
    {
        return Tuple.Create(
            new Triangle(new List<ValuedPoint> { a, b, center }),
            new Triangle(new List<ValuedPoint> { b, c, center }),
            new Triangle(new List<ValuedPoint> { c, d, center }),
            new Triangle(new List<ValuedPoint> { d, a, center })
        );
    }
}

// CurveTracer class
public class CurveTracer
{
    private List<Triangle> triangles;
    private Func fn;
    private double[] tol;
    private List<ValuedPoint> activeCurve = new List<ValuedPoint>();

    public CurveTracer(List<Triangle> triangles, Func fn, double[] tol)
    {
        this.triangles = triangles;
        this.fn = fn;
        this.tol = tol;
    }

    public List<List<Point>> Trace()
    {
        var curves = new List<List<ValuedPoint>>();

        foreach (var triangle in triangles)
        {
            if (!triangle.Visited && triangle.Next != null)
            {
                activeCurve = new List<ValuedPoint>();
                MarchTriangle(triangle);
                curves.Add(activeCurve);
            }
        }

        return curves.Select(curve => curve.Select(v => v.Pos).ToList()).ToList();
    }

    private void MarchTriangle(Triangle triangle)
    {
        var startTriangle = triangle;
        bool closedLoop = false;

        // Iterate backwards to start
        while (triangle.Prev != null)
        {
            triangle = triangle.Prev;
            if (triangle == startTriangle)
            {
                closedLoop = true;
                break;
            }
        }

        // March forward
        Triangle? currentTriangle = triangle;
        while (currentTriangle != null && !currentTriangle.Visited)
        {
            if (currentTriangle.NextBisectPoint != null)
                activeCurve.Add(currentTriangle.NextBisectPoint);

            currentTriangle.Visited = true;
            currentTriangle = currentTriangle.Next;
        }

        if (closedLoop && activeCurve.Count > 0)
        {
            activeCurve.Add(activeCurve[0]);
        }
    }
}

// Main plotting function and helpers
public static class IsolinePlotter
{
    public static List<List<Point>> PlotIsoline(
        Func fn,
        Point pmin,
        Point pmax,
        int minDepth = 5,
        int maxQuads = 10000,
        double[]? tol = null)
    {
        if (tol is null)
        {
            Point diff = pmax - pmin;
            tol = new double[diff.Coordinates.Length];
            for (int i = 0; i < tol.Length; i++)
                tol[i] = diff[i] / 1000.0;
        }
        Cell quadtree = BuildTree(2, fn, pmin, pmax, minDepth, maxQuads, tol);
        List<Triangle> triangles = new Triangulator(quadtree, fn, tol).Triangulate();
        List<List<Point>> isolines = new CurveTracer(triangles, fn, tol).Trace();
        return isolines;
    }

    public static List<List<Point>> PlotIsoline(
        Func fn,
        Point pmin,
        Point pmax,
        out Cell quadtree,
        out List<Triangle> triangles,
        int minDepth = 5,
        int maxQuads = 10000,
        double[]? tol = null)
    {
        if (tol is null)
        {
            Point diff = pmax - pmin;
            tol = new double[diff.Coordinates.Length];
            for (int i = 0; i < tol.Length; i++)
                tol[i] = diff[i] / 1000.0;
        }
        quadtree = BuildTree(2, fn, pmin, pmax, minDepth, maxQuads, tol);
        triangles = new Triangulator(quadtree, fn, tol).Triangulate();
        List<List<Point>> isolines = new CurveTracer(triangles, fn, tol).Trace();
        return isolines;
    }

    private static Cell BuildTree(
        int dim,
        Func fn,
        Point pmin,
        Point pmax,
        int minDepth,
        int maxCells,
        double[] tol)
    {
        int branchingFactor = 1 << dim;
        maxCells = Math.Max((int)Math.Pow(branchingFactor, minDepth), maxCells);

        var vertices = VerticesFromExtremes(dim, pmin, pmax, fn);
        var root = new Cell(dim, vertices, 0, new List<Cell>(), null, 0);

        var quadQueue = new Queue<Cell>();
        quadQueue.Enqueue(root);
        int leafCount = 1;

        while (quadQueue.Count > 0 && leafCount < maxCells)
        {
            var currentQuad = quadQueue.Dequeue();
            if (currentQuad.Depth < minDepth || ShouldDescendDeepCell(currentQuad, tol))
            {
                currentQuad.ComputeChildren(fn);
                foreach (var child in currentQuad.Children)
                    quadQueue.Enqueue(child);

                leafCount += branchingFactor - 1;
            }
        }

        return root;
    }

    private static bool ShouldDescendDeepCell(Cell cell, double[] tol)
    {
        var diff = cell.Vertices[cell.Vertices.Count - 1].Pos - cell.Vertices[0].Pos;
        bool tooSmall = true;
        for (int i = 0; i < diff.Coordinates.Length; i++)
        {
            if (diff[i] >= 10 * tol[i])
            {
                tooSmall = false;
                break;
            }
        }

        if (tooSmall) return false;

        bool allNaN = cell.Vertices.All(v => double.IsNaN(v.Val));
        if (allNaN) return false;

        bool anyNaN = cell.Vertices.Any(v => double.IsNaN(v.Val));
        if (anyNaN) return true;

        double firstSign = Math.Sign(cell.Vertices[0].Val);
        return cell.Vertices.Skip(1).Any(v => Math.Sign(v.Val) != firstSign);
    }

    private static List<ValuedPoint> VerticesFromExtremes(int dim, Point pmin, Point pmax, Func fn)
    {
        var w = pmax - pmin;
        var vertices = new List<ValuedPoint>();

        for (int i = 0; i < (1 << dim); i++)
        {
            var coords = new double[dim];
            for (int d = 0; d < dim; d++)
            {
                coords[d] = pmin[d] + ((i >> d) & 1) * w[d];
            }
            vertices.Add(new ValuedPoint(new Point(coords)).Calc(fn));
        }

        return vertices;
    }

    public static (ValuedPoint point, bool isZero) BinarySearchZero(
        ValuedPoint p1,
        ValuedPoint p2,
        Func fn,
        double[] tol)
    {
        var diff = p2.Pos - p1.Pos;
        bool converged = true;
        for (int i = 0; i < diff.Coordinates.Length; i++)
        {
            if (Math.Abs(diff[i]) >= tol[i])
            {
                converged = false;
                break;
            }
        }

        if (converged)
        {
            var pt = ValuedPoint.IntersectZero(p1, p2, fn);
            bool isZero = pt.Val == 0 || (
                Math.Sign(pt.Val - p1.Val) == Math.Sign(p2.Val - pt.Val) &&
                Math.Abs(pt.Val) < 1e200
            );
            return (pt, isZero);
        }
        else
        {
            var mid = ValuedPoint.Midpoint(p1, p2, fn);
            if (mid.Val == 0)
                return (mid, true);
            else if ((mid.Val > 0) == (p1.Val > 0))
                return BinarySearchZero(mid, p2, fn, tol);
            else
                return BinarySearchZero(p1, mid, fn, tol);
        }
    }
}

// Example usage
public class Example
{
    public static void MainExample2()
    {
        var parser = new MathExpressionParser.MathParser();

        var f = parser.Parse<Func<double, double, double>>("f(x,y) = x^2 + y^2 - 1");
        // Example: Find multiple circles
        Func multiCircleFunc = (double[] p) => f(p[0], p[1]);


        double xMin = -3;
        double yMin = -3;
        double xMax = 3;
        double yMax = 3;

        var pmin = new Point(xMin, yMin);
        var pmax = new Point(xMax, xMax);
        List<List<Point>> curves = IsolinePlotter.PlotIsoline(multiCircleFunc, pmin, pmax, 3, 1000);


        Console.WriteLine($"Found {curves.Count} curves");
        foreach (var curve in curves)
        {
            Console.WriteLine($"Curve with {curve.Count} points");
            foreach (Point point in curve.Take(5)) // Show first 5 points
            {
                Console.WriteLine($"  ({point[0]:F3}, {point[1]:F3})");
            }
            if (curve.Count > 5)
                Console.WriteLine("  ...");
        }


        Plot plot = new();

        plot.Axes.SetLimits(xMin, xMax, yMin, yMax);
        plot.Grid.MajorLineWidth = 1;
        plot.Grid.MajorLineColor = Colors.Gray.WithAlpha(0.3);

        Color color = RandomColor();
        ScottPlot.Plottables.Scatter[] scatters = new ScottPlot.Plottables.Scatter[curves.Count];

        for (int curveIndex = 0; curveIndex < curves.Count; curveIndex++)
        {
            List<Point> curve = curves[curveIndex];
            double[] x = new double[curve.Count];
            double[] y = new double[curve.Count];

            for (int pointIndex = 0; pointIndex < curve.Count; pointIndex++)
            {
                Point point = curve[pointIndex];
                x[pointIndex] = point[0];
                y[pointIndex] = point[1];
            }

            scatters[curveIndex] = plot.Add.Scatter(x, y);
            scatters[curveIndex].Color = color;
            scatters[curveIndex].MarkerSize = 3;
        }

        string fileName = "scatter_plot.png";
        plot.SavePng(fileName, 800, 600);

        //UpdatedExample.Main2();

        //FastCurveIntersectionExample.RunComplexExample2();
    }


    private static readonly Random _rand = new Random();
    public static Color RandomColor() =>
       new Color(255, _rand.Next(256), _rand.Next(256), _rand.Next(256));
}

