using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using ReadOutTestConsole;
using System.Numerics;
using MathNet.Numerics;


namespace ReadoutWinform
{
    public partial class MainForm : Form
    {
        public List<double> Frequency;
        //private DataContainer tmpcontainer;
        private DCWrapper tmpcontainer;
        private int tmpcontainerpos;
        private Queue<double> displayqueue;
        private Queue<double> anglequeue;
        private Queue<Complex> cqueue;

        private bool keepRunning;
        private bool measuring;
        private const int dc_length = 5000;

        // TOD Display settings
        private int xAxisDisplaySpacing;
        private bool yAxisFix;
        private double yAxisTop;
        private double yAxisBottom;

        private int xGridNumber; // TO BE IMPLEMENTED
        private int yGridNumber; // TO BE IMPLEMENTED
        private int xGridTmpPos;
        private Font gridFont = new Font("MS UI Gothic", 10);

        // for sweep
        private double sweepStartFreq;
        private double sweepEndFreq;
        private int sweepCount;

        private List<double> tmpIs;
        private List<double> tmpQs;
        private List<Complex> tmpCs;
        private List<double> tmpabs;
        private double sweepAbsMax;
        private double sweepAbsMin;

        // sweep fit
        private double angle_to_subtract;
        private double[] p_sub;
        private double[] p_center;
        private List<Complex> tmpCs_sub;
        private List<Complex> tmpCs_centered;
        private bool angle_parameter_set = false;

        // for writing
        private string directory_path;
        private DateTime epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime();

        // for debug
        private Complex fix_C;

        public MainForm()
        {
            Frequency = new List<double>();
            displayqueue = new Queue<double>();
            anglequeue = new Queue<double>();
            cqueue = new Queue<Complex>();

            // TOD display settings
            xAxisDisplaySpacing = 1;
            yAxisFix = false;
            yAxisTop = 100;
            yAxisBottom = 0;
            xGridNumber = 3;
            yGridNumber = 3;
            xGridTmpPos = 0;

            sweepCount = 101;
            

            // For DEBUG
            Frequency.Add(6.55e6); // resonance
            Frequency.Add(10e6); // off resonance
            // For DEBUG FIN
            InitializeComponent();
            axisPanel.Controls.Add(DataViewPanel);            
            ShowSettings();

            keepRunning = false;
            measuring = false;
            DataViewPanel.Paint += new PaintEventHandler(PanelDraw);
            tmpIQPanel.Paint += new PaintEventHandler(tmpIQPaint);
            axisPanel.Paint += new PaintEventHandler(axDraw);

            sweepAbsPanel.Paint += new PaintEventHandler(sweepAbsDraw);
            sweepUpperIQPanel.Paint += new PaintEventHandler(sweepUpperIQDraw);
            sweepBottomIQPanel.Paint += new PaintEventHandler(sweepBottomIQDraw);
            
            // write
            directory_path = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "tmpdata");

            ReadoutStatus.Text = "Ready";
        }


        private void startMeasurement()
        {            
            if (measuring)
                return;            
                        
            var ow = OutputWave.Instance;
            ow.SetFrequency(0, Frequency[0], 45.0, false);
            ow.SetFrequency(1, Frequency[1], 225.0, true);

            var rbcp = RBCP.Instance;
            var readout = Readout.Instance;

            keepRunning = true;
            measuring = true;

            readout.Clean();
            Thread.Sleep(50); //!!! HARD CODED !!!
            readout.Clean();

            rbcp.ToggleIQDataGate(true);
            while (keepRunning)
            {                
                var dcwrapper = new DCWrapper(dc_length);
                dcwrapper.SDCount = Program.SoftwareDownsampleCount;
                Program.DataContainers.Add(dcwrapper);
                dcwrapper.StartDateTime = DateTime.Now;                                
                readout.Read(dcwrapper, dcwrapper.Length);                
            }
            rbcp.ToggleIQDataGate(false);

            measuring = false;
            //StartButton.Enabled = true;
        }

        public void ShowSettings()
        {
            SDTextBox.Text = Program.SoftwareDownsampleCount.ToString();
            xAxisDisplaySpacingTextBox.Text = xAxisDisplaySpacing.ToString();
            YAxisFixComboBox.SelectedIndex = Convert.ToInt32(!yAxisFix);
            yTopTextBox.Text = yAxisTop.ToString();
            yBottomTextBox.Text = yAxisBottom.ToString();
            if (!yAxisFix)
            {
                yTopTextBox.Enabled = false;
                yBottomTextBox.Enabled = false;
            }
            freqTextBox1.Text = Frequency[0].ToString();            
            freqTextBox2.Text = Frequency[1].ToString();

            // sweep panel
            sweepStartFreqTextbox.Text = (Frequency[0] - 1e6).ToString();
            sweepEndFreqTextbox.Text = (Frequency[0] + 1e6).ToString();
            sweepCountTextbox.Text = sweepCount.ToString();
        }

        public void ReadSettings()
        {            
            try
            {
                Program.SoftwareDownsampleCount = Convert.ToInt32(SDTextBox.Text);
                xAxisDisplaySpacing = Convert.ToInt32(xAxisDisplaySpacingTextBox.Text);
                yAxisFix = Convert.ToBoolean(YAxisFixComboBox.Text);
                if (yAxisFix)
                {
                    yAxisTop = Convert.ToDouble(yTopTextBox.Text);
                    yAxisBottom = Convert.ToDouble(yBottomTextBox.Text);                    
                }
                Frequency[0] = Convert.ToDouble(freqTextBox1.Text);
                Frequency[1] = Convert.ToDouble(freqTextBox2.Text);
            } catch (Exception)
            {
                MessageBox.Show("Parse error", "Parse error",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }

        private void PanelDraw( object sender, PaintEventArgs e)
        {
            if (displayqueue.Count == 0)
                return;
            var g = e.Graphics;
            if (!yAxisFix)
            {
                yAxisTop = displayqueue.Max();
                yAxisBottom = displayqueue.Min();
            }            
            var factor = DataViewPanel.Height / (yAxisTop-yAxisBottom);
            var points = displayqueue.Select((v, i) => new Point(i*xAxisDisplaySpacing, (int)(DataViewPanel.Height - factor * (v - yAxisBottom))));
            g.DrawLines(Pens.Blue, points.ToArray());
            if (angle_parameter_set && anglequeue.Count > 1)
            {
                var anglemax = anglequeue.Max();
                var anglemin = anglequeue.Min();
                //var anglefactor = DataViewPanel.Height / Math.PI / 2;
                var anglefactor = DataViewPanel.Height / (anglemax - anglemin);
                //var angleoffset = DataViewPanel.Height / 2;

                //var anglepoints = anglequeue.Select((v, i) => new Point(i * xAxisDisplaySpacing, (int)(DataViewPanel.Height - anglefactor * v - angleoffset)));
                var anglepoints = anglequeue.Select((v, i) => new Point(i * xAxisDisplaySpacing, (int)(DataViewPanel.Height - anglefactor * (v - anglemin))));
                g.DrawLines(Pens.Green, anglepoints.ToArray());
            }

            // Grids
            var g_pen = new Pen(Color.Red, 1);
            g_pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            for(int i = 0; i < xGridNumber; i++)
            {
                var tmploc = (xGridTmpPos + DataViewPanel.Width / xGridNumber * i) % DataViewPanel.Width;
                var red_tmploc = (tmploc + DataViewPanel.Width) % DataViewPanel.Width;
                g.DrawLine(g_pen, new Point(red_tmploc, DataViewPanel.Height), new Point(red_tmploc, 0));
            }
            for(int i = 0; i < yGridNumber; i++)
            {
                var tmploc = DataViewPanel.Height / (yGridNumber + 1) * (i + 1);
                var realval = (DataViewPanel.Height - tmploc) / factor + yAxisBottom;
                g.DrawLine(g_pen, new Point(0, tmploc), new Point(DataViewPanel.Width, tmploc));
                g.DrawString(realval.ToString(), gridFont, Brushes.Red, 0, tmploc + 10);
            }
        }

        private void axDraw( object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            var vertical_top = new Point(DataViewPanel.Location.X - axisPanel.Location.X, 0);
            var vertical_bottom = new Point(DataViewPanel.Location.X - axisPanel.Location.X, axisPanel.Height);
            var horizontal_left = new Point(0, DataViewPanel.Height);
            var horizontal_right = new Point(axisPanel.Width, DataViewPanel.Height);
            g.DrawLine(Pens.Black, vertical_top, vertical_bottom);
            g.DrawLine(Pens.Black, horizontal_left, horizontal_right);
        }

        private bool first_draw = true;

        private void tmpIQPaint(object sender, PaintEventArgs e)
        {
            var g = e.Graphics;
            if (cqueue.Count < 1)
                return;
            var margin = 0.2;
            var factor = tmpIQPanel.Height / 2.0 * (1 - margin) / p_center[2];
            var offset = tmpIQPanel.Height / 2;
            g.DrawEllipse(Pens.Orange, (float) (margin / 2) * tmpIQPanel.Width,
                                       (float) (margin / 2) * tmpIQPanel.Height, 
                                       (float) (1 - margin) * tmpIQPanel.Width, 
                                       (float) (1 - margin) * tmpIQPanel.Height);
            var point_pen = new Pen(Color.Red, 5);
            var tmpc = cqueue.Last();
            g.DrawEllipse(point_pen, (float)(factor*tmpc.Real  + offset), (float)(-factor*tmpc.Imaginary + offset),
                                     3, 3);
            if (first_draw)
            {
                Console.WriteLine("R: {0}", p_center[2]);
                Console.WriteLine("factor: {0}", factor);
                Console.WriteLine("tmpmag: {0}", tmpc.Magnitude);
                Console.WriteLine("Draw X:{0} Y:{1}", (float)(tmpc.Real / factor + offset), (float)(tmpc.Imaginary / factor + offset));
                first_draw = false;
            }
        }

        private void DisplayQueueManipulation()
        {
            var newpoints = tmpcontainer.SDIndex - tmpcontainerpos;
            Enumerable.Range(tmpcontainerpos, newpoints).ToList().ForEach(i =>
            {
                var tmpi = tmpcontainer.SDIQArray[0].Is[i];
                var tmpq = tmpcontainer.SDIQArray[0].Qs[i];
                var tmpc = new Complex(tmpi, tmpq);
                displayqueue.Enqueue(tmpc.Magnitude);
                while (displayqueue.Count > DataViewPanel.Width / xAxisDisplaySpacing)
                {
                    displayqueue.Dequeue();
                    xGridTmpPos -= xAxisDisplaySpacing;
                }

                if (angle_parameter_set)
                {                    
                    while (anglequeue.Count < displayqueue.Count - 1)
                    {
                        anglequeue.Enqueue(0); cqueue.Enqueue(0);
                    }
                    var tmpc_mod = tmpc * Complex.Exp(-Complex.ImaginaryOne * angle_to_subtract) - (p_center[0] + p_center[1] * Complex.ImaginaryOne);
                    cqueue.Enqueue(tmpc_mod);
                    anglequeue.Enqueue((tmpc_mod).Phase);
                    while (anglequeue.Count > DataViewPanel.Width / xAxisDisplaySpacing)
                    {
                        anglequeue.Dequeue();                        
                    }
                }
            });
            tmpcontainerpos = tmpcontainer.SDIndex;            
        }

        private void DisplayRefreshTimer_Tick(object sender, EventArgs e)
        {
            var nowcontainer = (DCWrapper)Program.DataContainers.Last();
            if (tmpcontainer == null)
                tmpcontainer = nowcontainer;
            
            tmpcontainer.Convert();
            tmpcontainer.SoftwareDownsample(true);
            bool toberefreshed = false;
            if(tmpcontainer.SDIndex != tmpcontainerpos)
            {
                DisplayQueueManipulation();
                toberefreshed = true;
            }
            if (nowcontainer != tmpcontainer)
            {
                tmpcontainer = nowcontainer;
                tmpcontainerpos = 0;
                DisplayQueueManipulation();
                toberefreshed = true;
            }
            if(toberefreshed)
                DataViewPanel.Refresh();
        }

        private void StartButton_Click(object sender, EventArgs e)
        {
            if (measuring)
                return;
            DisplayRefreshTimer.Start();
            writeTimer.Start();
            Thread t = new Thread(new ThreadStart(startMeasurement));
            t.IsBackground = true;
            t.Start();
            ReadoutStatus.Text = "Running...";
        }

        private void StopButton_Click(object sender, EventArgs e)
        {
            keepRunning = false;
            ReadoutStatus.Text = "Stopped";
            writeTimer.Stop();
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            ;
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!Program.GUI_DEBUG)
            {
                RBCP.Instance.Close();
                Readout.Instance.Close();
                writer(count: 0);
            }            
        }

        private void YAxisFixComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (YAxisFixComboBox.Text == "")
                return;
            if (Convert.ToBoolean(YAxisFixComboBox.Text))
            {
                yTopTextBox.Enabled = true;
                yBottomTextBox.Enabled = true;
            }
            else
            {
                yTopTextBox.Enabled = false;
                yBottomTextBox.Enabled = false;
            }
        }

        private void settingApplyButton_Click(object sender, EventArgs e)
        {
            ReadSettings();
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.FileName = "hoge.dat";
            sfd.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            sfd.Filter = "All Files | *.*";
            sfd.FilterIndex = 0;
            sfd.Title = "Save";
            if (sfd.ShowDialog() == DialogResult.OK)
            {
                using ( var stream = sfd.OpenFile() )
                using ( System.IO.StreamWriter sw = new System.IO.StreamWriter(stream))
                {
                    foreach (var dc in Program.DataContainers)
                    {
                        dc.Express().ForEach(line => sw.WriteLine(line));
                    }
                }
            }
        }

        private void refreshButton_Click(object sender, EventArgs e)
        {
            ShowSettings();
        }

        private void sweepAbsDraw(object sender, PaintEventArgs e)
        {
            var sm = SweepManager.Instance;
            if (sm.Is[0].Count <= 1)
                return;

            var g = e.Graphics;            
            var points = tmpabs.Select((v, index) =>
            {
                var tmpfreq = sm.SweepFrequency[0][index];
                var xpos = (tmpfreq - sweepStartFreq) / (sweepEndFreq - sweepStartFreq) * sweepAbsPanel.Width;
                var ypos = (1- (v - sweepAbsMin) / (sweepAbsMax - sweepAbsMin)) * sweepAbsPanel.Height;
                return new Point((int)xpos, (int)ypos);
            });
            g.DrawLines(Pens.Black, points.ToArray());
        }

        private void sweepUpperIQDraw(object sender, PaintEventArgs e)
        {
            var sm = SweepManager.Instance;
            if (sm.Is[0].Count <= 1)
                return;

            var g = e.Graphics;
            var w = sweepUpperIQPanel.Width;
            var h = sweepUpperIQPanel.Height;
            var raw_points = tmpCs.Select(c => new Point((int)(c.Real / sweepAbsMax * w/2 + w/2),
                                                         (int)(c.Imaginary / sweepAbsMax * h/2 + h/2))).ToArray();            
            g.DrawLines(Pens.Blue, raw_points);
            var fix_near = (int)((Frequency[0] - sweepStartFreq) / (sweepEndFreq - sweepStartFreq) * sweepCount);
            if(raw_points.Length > fix_near)
                g.DrawEllipse(Pens.Red, raw_points[fix_near].X, raw_points[fix_near].Y, 8, 8);
            g.DrawEllipse(Pens.Green, (int)(fix_C.Real/sweepAbsMax*w/2 + w/2), (int)(fix_C.Imaginary / sweepAbsMax * h/2 + h/2), 8, 8);

            if (tmpCs_sub != null)
            {
                g.DrawLines(Pens.Red, 
                            tmpCs_sub.Select(c => new Point((int)(c.Real / sweepAbsMax * w / 2 + w / 2),
                                                            (int)(c.Imaginary / sweepAbsMax * h / 2 + h / 2)
                                                            )
                                            ).ToArray()
                            );                
            }
        }

        private void sweepBottomIQDraw(object sender, PaintEventArgs e)
        {
            if (tmpCs_centered != null)
            {
                var tccabs = tmpCs_centered.Select(c => c.Magnitude);
                var tccmax = tccabs.Max();

                var g = e.Graphics;
                var w = sweepBottomIQPanel.Width;
                var h = sweepBottomIQPanel.Height;
                var fix_near = (int)((Frequency[0] - sweepStartFreq) / (sweepEndFreq - sweepStartFreq)*sweepCount);
                var points = tmpCs_centered.Select(c => new Point((int)(c.Real / tccmax * w / 2 + w / 2),
                                                                 (int)(c.Imaginary / tccmax * h / 2 + h / 2)
                                                            )
                                                  ).ToArray();
                g.DrawLines(Pens.Green, points);
                g.DrawEllipse(Pens.Red, points[fix_near].X, points[fix_near].Y, 8, 8);
                Console.WriteLine("{0} {1}", tmpCs[fix_near].Real, tmpCs[fix_near].Imaginary);
                Console.WriteLine("{0} {1}", fix_C.Real, fix_C.Imaginary);
                var handmod = fix_C * Complex.Exp(-Complex.ImaginaryOne * (p_sub[0] * fix_near + p_sub[1])) - (p_center[0] + p_center[1]*Complex.ImaginaryOne);
                var handmod_as = fix_C * Complex.Exp(-Complex.ImaginaryOne * (angle_to_subtract)) - (p_center[0] + p_center[1] * Complex.ImaginaryOne);
                Console.WriteLine("angle_to_subtract: {0}", angle_to_subtract);
                Console.WriteLine("angle_to_subtract real val: {0}", p_sub[0] * fix_near + p_sub[1]);
                Console.WriteLine("angle_to_subtract real val: {0}", p_sub[0] * (fix_near + 1) + p_sub[1]);
                Console.WriteLine("angle_to_subtract real val: {0}", p_sub[0] * (fix_near - 1) + p_sub[1]);
                g.DrawEllipse(Pens.Black, (int)(handmod.Real / tccmax * w / 2 + w / 2), 
                                          (int)(handmod.Imaginary / tccmax * h / 2 + h / 2), 
                                          4, 4);
                g.DrawEllipse(Pens.Blue, (int)(handmod_as.Real / tccmax * w / 2 + w / 2),
                                          (int)(handmod_as.Imaginary / tccmax * h / 2 + h / 2),
                                          4, 4);
            }
        }


        private void sweepStartButton_Click(object sender, EventArgs e)
        {
            if (measuring)
            {
                MessageBox.Show("Blocked", "Blocked by other processes",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Error);
                return;
            }                

            try
            {
                sweepStartFreq = Convert.ToDouble(sweepStartFreqTextbox.Text);
                sweepEndFreq = Convert.ToDouble(sweepEndFreqTextbox.Text);
                sweepCount = Convert.ToInt32(sweepCountTextbox.Text);
            }
            catch
            {
                MessageBox.Show("Parse error", "Parse error",
                               MessageBoxButtons.OK,
                               MessageBoxIcon.Error);
                return;
            }

            var sm = SweepManager.Instance;
            if(sm.Is[0].Count != 0)
                sm.Clear();

            sm.SetFrequency(new double[] { Frequency[0], Frequency[1] },
                            new double[] { 0, 0 },
                            1);
            sm.Sweep();

            fix_C = sm.Is[0][0] + Complex.ImaginaryOne * sm.Qs[0][0];
            Console.WriteLine("fix_C: {0} + i*{1}", fix_C.Real, fix_C.Imaginary);
            sm.Clear();

            sm.SetFrequency(new double[] { sweepStartFreq, Frequency[1] },
                            new double[] { (sweepEndFreq - sweepStartFreq) / (sweepCount - 1), 0 },
                            sweepCount);

            sweepDisplayRefreshTimer.Enabled = true;
            Thread t = new Thread(new ThreadStart(doSweep));
            t.IsBackground = true;
            t.Start(); 
        }

        private void doSweep()
        {
            var sm = SweepManager.Instance;
            measuring = true;
            sm.Sweep();
            measuring = false;
            
                       
        }

        private void sweepDisplayRefreshTimer_Tick(object sender, EventArgs e)
        {
            var sm = SweepManager.Instance;
            if (sm.Is[0].Count <= 1)
                return;
                        
            tmpIs = sm.Is[0].ToList();
            tmpQs = sm.Qs[0].ToList();
            tmpCs = tmpIs.Zip(tmpQs, Tuple.Create).Select(iq => new Complex(iq.Item1, iq.Item2)).ToList();
            tmpabs = tmpCs.Select(c => c.Magnitude).ToList();
            sweepAbsMax = tmpabs.Max();
            sweepAbsMin = tmpabs.Min();
            // Draw
            sweepAbsPanel.Refresh();
            sweepUpperIQPanel.Refresh();
        }

        private void Fit()
        {
            var sm = SweepManager.Instance;
            if (sm.Is[0].Count <= 1)
                return;
            using (var sw = new System.IO.StreamWriter("sweep.dat"))
            {
                for (int i = 0; i < sm.SweepPoints; i++)
                {
                    sw.WriteLine("{0} {1} {2}", sm.SweepFrequency[0][i], sm.Is[0][i], sm.Qs[0][i]);
                }
            }

            System.Diagnostics.Process p = new System.Diagnostics.Process();                        
            p.StartInfo.FileName = "python";
            p.StartInfo.Arguments = "iqfit.py sweep.dat" + " " + Frequency[0].ToString();                        
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardInput = false;
            p.StartInfo.CreateNoWindow = true;            
            
            p.Start();
            string results_str = p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            p.Close();

            var results = results_str.Split('\n');
            try
            {
                angle_to_subtract = Convert.ToDouble(results[0]);
                p_sub    = results[1].Split().Select(str => Convert.ToDouble(str)).ToArray();
                p_center = results[2].Split().Select(str => Convert.ToDouble(str)).ToArray();
            }
            catch {
                MessageBox.Show("Fit failed", "Fit failed",
                           MessageBoxButtons.OK,
                           MessageBoxIcon.Error);
                return;
            }
            tmpCs_sub = tmpCs.Select((c, i) => c * Complex.Exp(-Complex.ImaginaryOne * (p_sub[0] * i + p_sub[1]))).ToList();
            tmpCs_centered = tmpCs_sub.Select(c => c - (p_center[0] + p_center[1]*Complex.ImaginaryOne)).ToList();

            angle_parameter_set = true;
        }

        private void sweepFitButton_Click(object sender, EventArgs e)
        {
            Fit();
            sweepUpperIQPanel.Refresh();
            sweepBottomIQPanel.Refresh();            
        }

        private bool writing = false;

        private void writeTimer_Tick(object sender, EventArgs e)
        {
            if (!writing)
            {
                if(Program.MY_DEBUG)
                    Console.WriteLine("Start to write!");
                writing = true;
                Thread t = new Thread(new ThreadStart(ringwrite));
                t.IsBackground = true;
                t.Start();
            }            
        }

        private void ringwrite()
        {
            writer(count: 10);
        }

        private void writer(int count = 10)
        {
            if(Program.MY_DEBUG)
                Console.WriteLine("We have {0} containers", Program.DataContainers.Count);

            while (Program.DataContainers.Count > count)
            {
                if(Program.MY_DEBUG)
                    Console.WriteLine("tmpcount: {0}", Program.DataContainers.Count);
                var dc = Program.DataContainers[0];
                var dt = dc.StartDateTime;
                var file_path = System.IO.Path.Combine(directory_path, "tmpdata" + ((int)(dt - epoch).TotalSeconds).ToString() + ".dat");
                using(var sw = new System.IO.StreamWriter(file_path))
                {                                        
                    dc.Express().ForEach(line => sw.WriteLine(line));                                        
                }
                Program.DataContainers.RemoveAt(0);
            }
            writing = false;
        }
    }
}
