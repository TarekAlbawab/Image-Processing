using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;

using AForge;                      //AForge.NET library
using AForge.Controls;
using AForge.Imaging;
using AForge.Imaging.Formats;
using AForge.Imaging.Filters;
using AForge.Imaging.Textures;
using AForge.Math;
using AForge.Math.Geometry;

using Color = System.Drawing.Color;
using Image = System.Drawing.Image;  
using Point = AForge.Point;
using Pen = System.Drawing.Pen;
using Rectangle = System.Drawing.Rectangle; 

namespace Image_Comparison
{
    public partial class Form1 : Form
    {
        private Bitmap referenceImage;
        private Bitmap shiftedImage;
        private int xRef;
        private int yRef;
        private int xShift;
        private int yShift;
        
        public Form1()
        {
            InitializeComponent();
            lblVerif.Text = string.Empty;
        }

        private void button1_Click(object sender, EventArgs e)
        {          
            // Displays a standard dialog box that prompts the user to open a file
            OpenFileDialog Openfile = new OpenFileDialog();
            if (Openfile.ShowDialog() == DialogResult.OK)
            {
                referenceImage = (Bitmap)Image.FromFile(Openfile.FileName);
                pictureBox1.Image = referenceImage;

                // Display height and width of image used
                textBox1.Text = referenceImage.Width.ToString();
                textBox2.Text = referenceImage.Height.ToString();

                // collect statistics
                ImageStatistics refHistogram = new ImageStatistics(referenceImage);
                // display histogram for reference image
                histogram1.Values = refHistogram.GrayWithoutBlack.Values;

            }
        }

        private void button4_Click(object sender, EventArgs e)
        {
            // Displays a standard dialog box that prompts the user to open a file
            OpenFileDialog Openfile = new OpenFileDialog();
            if (Openfile.ShowDialog() == DialogResult.OK)
            {
                shiftedImage = (Bitmap)Image.FromFile(Openfile.FileName);
                pictureBox18.Image = shiftedImage;

                // Display height and width of image used
                textBox20.Text = shiftedImage.Width.ToString();
                textBox19.Text = shiftedImage.Height.ToString();

                // collect statistics
                ImageStatistics shfHistogram = new ImageStatistics(shiftedImage);
                // display histogram of shifted image
                histogram2.Values = shfHistogram.GrayWithoutBlack.Values;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            // REMOVE NOISE BEFORE PROCESSING //
            // Apply BilateralSmoothing to remove noise and get sharp edges //
            // create filter
            BilateralSmoothing Smoothing = new BilateralSmoothing();
            Smoothing.KernelSize = 9;             // value not finalize yet
            Smoothing.SpatialFactor = 10;          // value not finalize yet
            // apply the filter
            Smoothing.ApplyInPlace(referenceImage);

            // CONTRAST TO MAKE IMAGE BRIGHTER SPECIFICALLY THE CENTER CIRCLE //
            // create filter
            ContrastStretch contrast = new ContrastStretch();
            // process image
            contrast.ApplyInPlace(referenceImage);

            // THRESHOLDING using Bradley technique to get only edges //
            // create the filter
            BradleyLocalThresholding Thresholding = new BradleyLocalThresholding();
            // apply the filter
            Thresholding.ApplyInPlace(referenceImage);

            // EDGE DETECTION USING SOBEL EDGE DETECTOR. IMPORTANT FOR SHAPE DETECTION //
            // create filter
            SobelEdgeDetector edgeDetector = new SobelEdgeDetector();
            // apply the filter
            edgeDetector.ApplyInPlace(referenceImage);

            // DILATION TO THICKEN THE EDGES //
            // create filter
            Dilatation Dilation = new Dilatation();
            // apply the filter
            Dilation.ApplyInPlace(referenceImage);

            // generate different label colours for every component
            ConnectedComponentsLabeling connectedComponentFilter = new ConnectedComponentsLabeling();
            connectedComponentFilter.CoupledSizeFiltering = true;
            connectedComponentFilter.MinHeight = 40;
            connectedComponentFilter.MinWidth = 40;
            Bitmap connectedImage = connectedComponentFilter.Apply(referenceImage);
            pictureBox2.Image = connectedImage;

            // DETECT RECTANGLE AND CIRCLE //
            //Create a instance of blob counter algorithm
            BlobCounter _blobCounter = new BlobCounter();
            Bitmap tempBitmap = new Bitmap(connectedImage.Width, connectedImage.Height);

            //Configure Filter
            _blobCounter.MinWidth = 200;
            _blobCounter.MinHeight = 200;
            _blobCounter.MaxWidth = 1100;
            _blobCounter.MaxHeight = 1050;
            _blobCounter.FilterBlobs = true;

            _blobCounter.ProcessImage(connectedImage);
            Blob[] _blobPoints = _blobCounter.GetObjectsInformation();

            Graphics _g = Graphics.FromImage(tempBitmap);

            SimpleShapeChecker _shapeChecker = new SimpleShapeChecker();
            for (int i = 0; i < _blobPoints.Length; i++)
            {
                List<IntPoint> _edgePoint = _blobCounter.GetBlobsEdgePoints(_blobPoints[i]);
                List<IntPoint> _corners = null;
                AForge.Point _refCenter;
                float _radius;
                if (_shapeChecker.IsQuadrilateral(_edgePoint, out _corners))    // determine if quadrilateral exist or not
                {
                    System.Drawing.Font _font = new System.Drawing.Font("Segoe UI", 40);
                    System.Drawing.SolidBrush _brush = new System.Drawing.SolidBrush(System.Drawing.Color.Red);
                    System.Drawing.Point[] _coordinates = ToPointsArray(_corners);
                    if (_coordinates.Length == 4)
                    {
                        int _x = _coordinates[0].X;
                        int _y = _coordinates[0].Y;
                        Pen _pen = new Pen(Color.Brown);
                        string _shapeString = "" + _shapeChecker.CheckShapeType(_edgePoint);
                        _g.DrawString(_shapeString, _font, _brush, _x, _y);
                        _g.DrawPolygon(_pen, ToPointsArray(_corners));
                    }
                }
                if (_shapeChecker.IsCircle(_edgePoint, out _refCenter, out _radius))    // determine if circle exist or not
                {
                    string _shapeString = "" + _shapeChecker.CheckShapeType(_edgePoint);
                    System.Drawing.Font _font = new System.Drawing.Font("Segoe UI", 40);
                    System.Drawing.SolidBrush _brush = new System.Drawing.SolidBrush(System.Drawing.Color.Green);
                    Pen _pen = new Pen(Color.GreenYellow);
                    xRef = (int)_refCenter.X;
                    yRef = (int)_refCenter.Y;
                    _g.DrawString(_shapeString, _font, _brush, xRef, yRef);
                    _g.DrawEllipse(_pen, (float)(_refCenter.X - _radius),
                                         (float)(_refCenter.Y - _radius),
                                         (float)(_radius * 2),
                                         (float)(_radius * 2));
                }
            }
            pictureBox3.Image = tempBitmap;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            // REMOVE NOISE BEFORE PROCESSING //
            // Apply BilateralSmoothing to remove noise and get sharp edges //
            // create filter
            AForge.Imaging.Filters.BilateralSmoothing Smoothing = new AForge.Imaging.Filters.BilateralSmoothing();
            Smoothing.KernelSize = 9;             // value not finalize yet
            Smoothing.SpatialFactor = 10;          // value not finalize yet
            // apply the filter
            Smoothing.ApplyInPlace(shiftedImage);

            // CONTRAST TO MAKE IMAGE BRIGHTER SPECIFICALLY THE CENTER CIRCLE //
            // create filter
            ContrastStretch contrast = new ContrastStretch();
            // process image
            contrast.ApplyInPlace(shiftedImage);

            // THRESHOLDING using Bradley technique to get only edges //
            // create the filter
            BradleyLocalThresholding Thresholding = new BradleyLocalThresholding();
            // apply the filter
            Thresholding.ApplyInPlace(shiftedImage);

            // EDGE DETECTION USING SOBEL EDGE DETECTOR. IMPORTANT FOR SHAPE DETECTION //
            // create filter
            SobelEdgeDetector edgeDetector = new SobelEdgeDetector();
            // apply the filter
            edgeDetector.ApplyInPlace(shiftedImage);

            // DILATION TO THICKEN THE EDGES //
            // create filter
            Dilatation Dilation = new Dilatation();
            // apply the filter
            Dilation.ApplyInPlace(shiftedImage);

            // generate different label colours for every component
            ConnectedComponentsLabeling connectedComponentFilter = new ConnectedComponentsLabeling();
            connectedComponentFilter.CoupledSizeFiltering = true;
            connectedComponentFilter.MinHeight = 40;
            connectedComponentFilter.MinWidth = 40;
            Bitmap connectedImage = connectedComponentFilter.Apply(shiftedImage);
            pictureBox17.Image = connectedImage;

            // DETECT RECTANGLE AND CIRCLE //
            //Create a instance of blob counter algorithm
            BlobCounter _blobCounter = new BlobCounter();
            Bitmap tempBitmap = new Bitmap(connectedImage.Width, connectedImage.Height);

            //Configure Filter
            _blobCounter.MinWidth = 200;
            _blobCounter.MinHeight = 200;
            _blobCounter.MaxWidth = 1100;
            _blobCounter.MaxHeight = 1050;
            _blobCounter.FilterBlobs = true;

            _blobCounter.ProcessImage(connectedImage);
            Blob[] _blobPoints = _blobCounter.GetObjectsInformation();

            Graphics _g = Graphics.FromImage(tempBitmap);

            SimpleShapeChecker _shapeChecker = new SimpleShapeChecker();
            for (int i = 0; i < _blobPoints.Length; i++)
            {
                List<IntPoint> _edgePoint = _blobCounter.GetBlobsEdgePoints(_blobPoints[i]);
                List<IntPoint> _corners = null;
                AForge.Point _shftCenter;
                float _radius;
                if (_shapeChecker.IsQuadrilateral(_edgePoint, out _corners))    // determine if quadrilateral exist or not
                {
                    System.Drawing.Font _font = new System.Drawing.Font("Segoe UI", 40);
                    System.Drawing.SolidBrush _brush = new System.Drawing.SolidBrush(System.Drawing.Color.Red);
                    System.Drawing.Point[] _coordinates = ToPointsArray(_corners);
                    if (_coordinates.Length == 4)
                    {
                        int _x = _coordinates[0].X;
                        int _y = _coordinates[0].Y;
                        Pen _pen = new Pen(Color.Brown);
                        string _shapeString = "" + _shapeChecker.CheckShapeType(_edgePoint);
                        _g.DrawString(_shapeString, _font, _brush, _x, _y);
                        _g.DrawPolygon(_pen, ToPointsArray(_corners));
                    }
                }
                if (_shapeChecker.IsCircle(_edgePoint, out _shftCenter, out _radius))   // determine if circle exist or not
                {
                    string _shapeString = "" + _shapeChecker.CheckShapeType(_edgePoint);
                    System.Drawing.Font _font = new System.Drawing.Font("Segoe UI", 40);
                    System.Drawing.SolidBrush _brush = new System.Drawing.SolidBrush(System.Drawing.Color.Green);
                    Pen _pen = new Pen(Color.GreenYellow);
                    xShift = (int)_shftCenter.X;
                    yShift = (int)_shftCenter.Y;
                    _g.DrawString(_shapeString, _font, _brush, xShift, yShift);
                    _g.DrawEllipse(_pen, (float)(_shftCenter.X - _radius),
                                         (float)(_shftCenter.Y - _radius),
                                         (float)(_radius * 2),
                                         (float)(_radius * 2));

                    double xDisplacement = getXdisplacementOfPointOnCircle(xShift);     // calculate x-axis displacement
                    double yDisplacement = getXdisplacementOfPointOnCircle(yShift);     // calculate y-axis displacment
                    double angle = getAngleOfPointOnCircle(xShift, yShift);             // calculate angle of shift

                    textBox21.Text = xDisplacement.ToString();                          // display x-axis displacement on a textbox
                    textBox24.Text = yDisplacement.ToString();                          // display y-axis displacement on a textbox
                    textBox22.Text = angle.ToString();                                  // display angle shifted on a textbox                  

                    if (xDisplacement <= 75 && yDisplacement <= 75 && angle <= 10)
                    {
                        lblVerif.Text = "✓ ACCEPT";
                        lblVerif.ForeColor = Color.Green;
                    }
                    else
                    {
                        lblVerif.Text = "X REJECT";
                        lblVerif.ForeColor = Color.Red;
                    }
                }
            }
            pictureBox16.Image = tempBitmap;
        }

        private void button5_Click(object sender, EventArgs e)
        {
            // create intersect filter
            Intersect findIntersect = new Intersect(shiftedImage);
            // apply the filter
            Bitmap resultImage = findIntersect.Apply(referenceImage);
            pictureBox4.Image = resultImage;
        }

        // Conver list of AForge.NET's points to array of .NET points
        private System.Drawing.Point[] ToPointsArray(List<IntPoint> points)
        {
            System.Drawing.Point[] array = new System.Drawing.Point[points.Count];

            for (int i = 0, n = points.Count; i < n; i++)
            {
                array[i] = new System.Drawing.Point(points[i].X, points[i].Y);
            }

            return array;
        }

        // return angle value
        public double getAngleOfPointOnCircle(double x, double y)
        {
            int centerX = xRef;
            int centerY = yRef;
            
            //calculate the circle radius
            double radius = Math.Sqrt(Math.Abs(centerX - x) * Math.Abs(centerX - x) + Math.Abs(centerY - y) * Math.Abs(centerY - y));
            // calculate the coordinates for the 3 o-clock point on the circle
            double p0x = radius-centerX;
            double p0y = centerY;
            // calculate and return the angle in degrees in the range 0..360
            return (2 * Math.Atan2(y - p0y, x - p0x)) * 180 / Math.PI;
        }

        // return x-displacement 
        public double getXdisplacementOfPointOnCircle(double x)
        {
            int centerX = xRef;
            return Math.Abs(x - centerX);
        }

        // return y-displacement
        public double getYdisplacementOfPointOnCircle(double y)
        {
            int centerY = yRef;
            return Math.Abs(y - centerY);
        }

        private void button7_Click(object sender, EventArgs e)      // display reference image properties on textboxes
        {
            double entropy, energy, contrast, homogeneity;

            AnalyseBitmapTexture(referenceImage, out entropy, out energy, out contrast, out homogeneity);
            ImageStatistics stats = new ImageStatistics(referenceImage);

            textBox3.Text = entropy.ToString();
            textBox4.Text = energy.ToString();
            textBox5.Text = homogeneity.ToString();
            textBox6.Text = contrast.ToString();
            textBox7.Text = xRef.ToString();
            textBox8.Text = yRef.ToString();
            textBox10.Text = stats.Gray.Values.Average().ToString();                     
        }

        private void button6_Click(object sender, EventArgs e)      // display shifted image properties on textboxes
        {
            double entropy, energy, contrast, homogeneity;

            AnalyseBitmapTexture(shiftedImage, out entropy, out energy, out contrast, out homogeneity);
            ImageStatistics stats = new ImageStatistics(shiftedImage);

            textBox18.Text = entropy.ToString();
            textBox17.Text = energy.ToString();
            textBox16.Text = homogeneity.ToString();
            textBox15.Text = contrast.ToString();
            textBox14.Text = xShift.ToString();
            textBox13.Text = yShift.ToString();
            textBox11.Text = stats.Gray.Values.Average().ToString();
        }
        // analyse image textures
        private void AnalyseBitmapTexture(Bitmap bitmap, out double entropy, out double energy, out double contrast, out double homogeneity)
        {
            GrayLevelCooccurrenceMatrix matrix = new GrayLevelCooccurrenceMatrix();
            double[,] glcm = matrix.Compute(UnmanagedImage.FromManagedImage(bitmap));
            entropy = CalculateEntropy(glcm);
            energy = CalculateEnergy(glcm);
            contrast = CalculateContrast(glcm);
            homogeneity = CalculateHomogeneity(glcm);
        }

        private double CalculateEntropy(double[,] glcm)     // calculate the entrophy value
        {
            double sum = 0;
            for (int i = 0; i < glcm.GetLength(0); i++)
            {
                for (int j = 0; j < glcm.GetLength(1); j++)
                {
                    if (Math.Abs(glcm[i, j]) > 0.0001)
                        sum += glcm[i, j] * Math.Log(glcm[i, j], 2);
                }
            }
            return -sum;
        }

        private double CalculateEnergy(double[,] glcm)      // calculate the energy value
        {
            double sum = 0;
            for (int i = 0; i < glcm.GetLength(0); i++)
            {
                for (int j = 0; j < glcm.GetLength(1); j++)
                {
                    sum += Math.Pow(glcm[i, j], 2);
                }
            }
            return sum;
        }

        private double CalculateContrast(double[,] glcm)      // calculate the contrast value
        {
            double sum = 0;
            for (int i = 0; i < glcm.GetLength(0); i++)
            {
                for (int j = 0; j < glcm.GetLength(1); j++)
                {
                    sum += Math.Pow(i - j, 2) * glcm[i, j];
                }
            }
            return sum;
        }

        private double CalculateHomogeneity(double[,] glcm)     // calculate the homogeneity value
        {
            double sum = 0;
            for (int i = 0; i < glcm.GetLength(0); i++)
            {
                for (int j = 0; j < glcm.GetLength(1); j++)
                {
                    sum += (glcm[i, j]) / (1 + Math.Abs(i - j));
                }
            }
            return sum;
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }

    // Accord Imaging Library
    // The Accord.NET Framework
    // http://accord-framework.net
    //
    // Copyright © Diego Catalano, 2013
    // diego.catalano at live.com
    //
    // Copyright © César Souza, 2009-2014
    // cesarsouza at gmail.com

    public enum CooccurrenceDegree
    {
        /// <summary>
        ///   Find co-occurrences at 0° degrees.
        /// </summary>
        /// 
        Degree0,

        /// <summary>
        ///   Find co-occurrences at 45° degrees.
        /// </summary>
        /// 
        Degree45,

        /// <summary>
        ///   Find co-occurrences at 90° degrees.
        /// </summary>
        /// 
        Degree90,

        /// <summary>
        ///   Find co-occurrences at 135° degrees.
        /// </summary>
        /// 
        Degree135
    };


    /// <summary>
    ///   Gray-Level Co-occurrence Matrix (GLCM).
    /// </summary>
    /// 
    public class GrayLevelCooccurrenceMatrix
    {

        private CooccurrenceDegree degree;
        private bool autoGray = true;
        private bool normalize = true;
        private int numPairs = 0;
        private int distance = 1;


        /// <summary>
        ///   Gets or sets whether the maximum value of gray should be
        ///   automatically computed from the image. If set to false,
        ///   the maximum gray value will be assumed 255.
        /// </summary>
        /// 
        public bool AutoGray
        {
            get { return autoGray; }
            set { autoGray = value; }
        }

        /// <summary>
        ///   Gets or sets whether the produced GLCM should be normalized,
        ///   dividing each element by the number of pairs. Default is true.
        /// </summary>
        /// 
        /// <value>
        ///   <c>true</c> if the GLCM should be normalized; otherwise, <c>false</c>.
        /// </value>
        /// 
        public bool Normalize
        {
            get { return normalize; }
            set { normalize = value; }
        }

        /// <summary>
        ///   Gets or sets the direction at which the co-occurrence should be found.
        /// </summary>
        /// 
        public CooccurrenceDegree Degree
        {
            get { return degree; }
            set { degree = value; }
        }

        /// <summary>
        ///   Gets or sets the distance at which the 
        ///   texture should be analyzed. Default is 1.
        /// </summary>
        /// 
        public int Distance
        {
            get { return distance; }
            set { distance = value; }
        }

        /// <summary>
        ///   Gets the number of pairs registered during the
        ///   last <see cref="Compute(UnmanagedImage)">computed GLCM</see>.
        /// </summary>
        /// 
        public int Pairs
        {
            get { return numPairs; }
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="GrayLevelCooccurrenceMatrix"/> class.
        /// </summary>
        /// 
        public GrayLevelCooccurrenceMatrix() { }

        /// <summary>
        ///   Initializes a new instance of the <see cref="GrayLevelCooccurrenceMatrix"/> class.
        /// </summary>
        /// 
        /// <param name="distance">The distance at which the texture should be analyzed.</param>
        /// 
        public GrayLevelCooccurrenceMatrix(int distance)
        {
            this.distance = distance;
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="GrayLevelCooccurrenceMatrix"/> class.
        /// </summary>
        /// 
        /// <param name="distance">The distance at which the texture should be analyzed.</param>
        /// <param name="degree">The direction to look for co-occurrences.</param>
        /// 
        public GrayLevelCooccurrenceMatrix(int distance, CooccurrenceDegree degree)
        {
            this.distance = distance;
            this.degree = degree;
        }

        /// <summary>
        ///   Initializes a new instance of the <see cref="GrayLevelCooccurrenceMatrix"/> class.
        /// </summary>
        /// 
        /// <param name="distance">The distance at which the texture should be analyzed.</param>
        /// <param name="degree">The direction to look for co-occurrences.</param>
        /// <param name="autoGray">Whether the maximum value of gray should be
        ///   automatically computed from the image. Default is true.</param>
        /// <param name="normalize">Whether the produced GLCM should be normalized,
        ///   dividing each element by the number of pairs. Default is true.</param>
        /// 
        public GrayLevelCooccurrenceMatrix(int distance, CooccurrenceDegree degree,
            bool normalize = true, bool autoGray = true)
        {
            this.distance = distance;
            this.degree = degree;
            this.normalize = normalize;
            this.autoGray = autoGray;
        }

        /// <summary>
        ///   Computes the Gray-level Co-occurrence Matrix (GLCM) 
        ///   for the given source image.
        /// </summary>
        /// 
        /// <param name="source">The source image.</param>
        /// 
        /// <returns>A square matrix of double-precision values containing
        /// the GLCM for the given <paramref name="source"/>.</returns>
        /// 
        public double[,] Compute(UnmanagedImage source)
        {
            return Compute(source, new Rectangle(0, 0, source.Width, source.Height));
        }

        /// <summary>
        ///   Computes the Gray-level Co-occurrence Matrix for the given matrix.
        /// </summary>
        /// 
        /// <param name="source">The source image.</param>
        /// <param name="region">A region of the source image where
        ///  the GLCM should be computed for.</param>
        /// 
        /// <returns>A square matrix of double-precision values containing the GLCM for the
        ///   <paramref name="region"/> of the given <paramref name="source"/>.</returns>
        /// 
        public unsafe double[,] Compute(UnmanagedImage source, Rectangle region)
        {
            int width = region.Width;
            int height = region.Height;
            int stride = source.Stride;
            int offset = stride - width;
            int maxGray = 255;

            int startX = region.X;
            int startY = region.Y;

            byte* src = (byte*)source.ImageData.ToPointer() + startY * stride + startX;

            if (autoGray)
                maxGray = max(width, height, offset, src);

            numPairs = 0;
            int size = maxGray + 1;
            double[,] cooccurrence = new double[size, size];


            switch (degree)
            {
                case CooccurrenceDegree.Degree0:
                    for (int y = startY; y < height; y++)
                    {
                        for (int x = startX + distance; x < width; x++)
                        {
                            byte a = src[stride * y + (x - distance)];
                            byte b = src[stride * y + x];
                            cooccurrence[a, b]++;
                            numPairs++;
                        }
                    }
                    break;

                case CooccurrenceDegree.Degree45:
                    for (int y = startY + distance; y < height; y++)
                    {
                        for (int x = startX; x < width - distance; x++)
                        {
                            byte a = src[stride * y + x];
                            byte b = src[stride * (y - distance) + (x + distance)];
                            cooccurrence[a, b]++;
                            numPairs++;
                        }
                    }
                    break;

                case CooccurrenceDegree.Degree90:
                    for (int y = startY + distance; y < height; y++)
                    {
                        for (int x = startX; x < width; x++)
                        {
                            byte a = src[stride * (y - distance) + x];
                            byte b = src[stride * y + x];
                            cooccurrence[a, b]++;
                            numPairs++;
                        }
                    }
                    break;

                case CooccurrenceDegree.Degree135:
                    for (int y = startY + distance; y < height; y++)
                    {
                        int steps = width - 1;
                        for (int x = startX; x < width - distance; x++)
                        {
                            byte a = src[stride * y + (steps - x)];
                            byte b = src[stride * (y - distance) + (steps - distance - x)];
                            cooccurrence[a, b]++;
                            numPairs++;
                        }
                    }
                    break;
            }

            if (normalize && numPairs > 0)
            {
                fixed (double* ptrMatrix = cooccurrence)
                {
                    double* c = ptrMatrix;
                    for (int i = 0; i < cooccurrence.Length; i++, c++)
                        *c /= numPairs;
                }
            }

            return cooccurrence;
        }

        unsafe private static int max(int width, int height, int offset, byte* src)
        {
            int maxGray = 0;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++, src++)
                    if (*src > maxGray) maxGray = *src;
                src += offset;
            }

            return maxGray;
        }
    }
}
