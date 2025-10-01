using netDxf;
using netDxf.Entities;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

//Avoid amibiguity, vector definition exists in both libraries
using SysVec2 = System.Numerics.Vector2;
using DxfVec2 = netDxf.Vector2;

namespace DXF_Raptor_Template_Reader
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();

            Log("Application started.");
        }

        // Run check on file type and then allow for uploading DXF file to be parsed. Only allows above 2012 versions. This error is handled in the DFX reader class
        // Within the DXF file replace the header tag $ACADVER with the version you want to use to match this. You can replace it with AC1032 as proven working in the example DXF file.

        private void btnUpload_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "DXF files (*.dxf)|*.dxf";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    string fileNameOnly = Path.GetFileNameWithoutExtension(filePath);
                    Log($"Selected file: {filePath}");


                    try
                    {
                        string jsonOutput = DXF_Reader.ParseDxfToJson(filePath, Log, out var debugLoops);

                        using (SaveFileDialog saveDialog = new SaveFileDialog())
                        {
                            saveDialog.Filter = "JSON files (*.json)|*.json";
                            // Change default naming to the uploaded DXF file with .json
                            saveDialog.FileName = (fileNameOnly + "_output.json");
                            DrawLoops(debugLoops);
                            Log("Loops drawn");
                            rtbStatus.AppendText(saveDialog.FileName);

                            if (saveDialog.ShowDialog() == DialogResult.OK)
                            {
                                System.IO.File.WriteAllText(saveDialog.FileName, jsonOutput);
                                Log("JSON successfully saved to: " + saveDialog.FileName);
                                MessageBox.Show("DXF parsed and saved.");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Log("Error: " + ex.Message);
                        MessageBox.Show("Error: " + ex.Message);
                    }
                }
            }
        }


        // Upload TQS JSON file to compare with DXF file. This will be used to check for discrepancies in the DXF file and the TQS structure. Only use for temporary proof of concept in code.
        private void btnUploadTQS_Click(object sender, EventArgs e)
        {
            using (OpenFileDialog openFileDialog = new OpenFileDialog())
            {
                openFileDialog.Filter = "JSON files (*.json)|*.json";
                if (openFileDialog.ShowDialog() == DialogResult.OK)
                {
                    string filePath = openFileDialog.FileName;
                    string fileNameOnly = Path.GetFileNameWithoutExtension(filePath);
                    Log($"Selected file: {filePath}");

                    try
                    {
                        // Read the JSON TQS strucutre file to then be used in comparison with the parsed DXF file. Output in system log matched result and discrepancies.
                        // Missing workop, additional piece could not find ect...
                    }
                    catch (Exception ex)
                    {
                        Log("Error: " + ex.Message);
                        MessageBox.Show("Error: " + ex.Message);
                    }
                }
            }
        }



        //Draw found loop polygons in picture box shape for debugging
        private void DrawLoops(List<List<SysVec2>> loops)
        {
            if (loops == null || loops.Count == 0) return;

            Bitmap bmp = new Bitmap(pictureBoxPolygonDraw.Width, pictureBoxPolygonDraw.Height);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                //Graphic pen colour options
                g.Clear(Color.White);
                Pen pen = new Pen(Color.Blue, 2);

                //Get bounds of all points
                float minX = float.MaxValue, maxX = float.MinValue;
                float minY = float.MaxValue, maxY = float.MinValue;

                foreach (var loop in loops)
                {
                    foreach (var point in loop)
                    {
                        minX = Math.Min(minX, point.X);
                        maxX = Math.Max(maxX, point.X);
                        minY = Math.Min(minY, point.Y);
                        maxY = Math.Max(maxY, point.Y);
                    }
                }

              
                float dx = maxX - minX;
                float dy = maxY - minY;

              
                if (dx == 0) dx = 1;
                if (dy == 0) dy = 1;

                float scaleX = (pictureBoxPolygonDraw.Width - 20) / dx;
                float scaleY = (pictureBoxPolygonDraw.Height - 20) / dy;
                float scale = Math.Min(scaleX, scaleY); 

                // Center the drawing in the PictureBox preventing any off shoots from picture box limit
                float offsetX = (pictureBoxPolygonDraw.Width - (dx * scale)) / 2f;
                float offsetY = (pictureBoxPolygonDraw.Height - (dy * scale)) / 2f;

                foreach (var loop in loops)
                {
                    if (loop.Count < 2) continue;

                    for (int i = 0; i < loop.Count; i++)
                    {
                        var start = loop[i];
                        var end = loop[(i + 1) % loop.Count];

                        // Apply scale and offset
                        var sx = (start.X - minX) * scale + offsetX;
                        var sy = pictureBoxPolygonDraw.Height - ((start.Y - minY) * scale + offsetY); // flip Y
                        var ex = (end.X - minX) * scale + offsetX;
                        var ey = pictureBoxPolygonDraw.Height - ((end.Y - minY) * scale + offsetY);   // flip Y

                        g.DrawLine(pen, sx, sy, ex, ey);
                    }
                }
            }

            pictureBoxPolygonDraw.Image = bmp;
        }

        // Log for status messages & debugging. Append to rtb status rich text box each debug message in DXF reader
        public void Log(string message)
        {
            rtbStatus.AppendText($"[{DateTime.Now:HH:mm:ss}] {message}\n");
        }
    }
}





