namespace DXF_Raptor_Template_Reader
{
    partial class Form1
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnUpload = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.rtbStatus = new System.Windows.Forms.RichTextBox();
            this.label4 = new System.Windows.Forms.Label();
            this.pictureBoxPolygonDraw = new System.Windows.Forms.PictureBox();
            this.btnUploadTQS = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPolygonDraw)).BeginInit();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Segoe UI", 36F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(611, 9);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(272, 65);
            this.label1.TabIndex = 0;
            this.label1.Text = "DXF Reader";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Segoe UI", 21.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(493, 74);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(501, 40);
            this.label2.TabIndex = 1;
            this.label2.Text = "Upload DXF File And Convert To JSON";
            // 
            // btnUpload
            // 
            this.btnUpload.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnUpload.Location = new System.Drawing.Point(55, 242);
            this.btnUpload.Margin = new System.Windows.Forms.Padding(4);
            this.btnUpload.Name = "btnUpload";
            this.btnUpload.Size = new System.Drawing.Size(321, 132);
            this.btnUpload.TabIndex = 2;
            this.btnUpload.Text = "Upload DXF";
            this.btnUpload.UseVisualStyleBackColor = true;
            this.btnUpload.Click += new System.EventHandler(this.btnUpload_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(402, 206);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(335, 32);
            this.label3.TabIndex = 3;
            this.label3.Text = "Status Log - System Messages";
            // 
            // rtbStatus
            // 
            this.rtbStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.rtbStatus.Location = new System.Drawing.Point(408, 242);
            this.rtbStatus.Margin = new System.Windows.Forms.Padding(4);
            this.rtbStatus.Name = "rtbStatus";
            this.rtbStatus.Size = new System.Drawing.Size(416, 375);
            this.rtbStatus.TabIndex = 4;
            this.rtbStatus.Text = "";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Segoe UI", 18F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(994, 206);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(288, 32);
            this.label4.TabIndex = 5;
            this.label4.Text = "System Polygon Drawings";
            // 
            // pictureBoxPolygonDraw
            // 
            this.pictureBoxPolygonDraw.BackColor = System.Drawing.SystemColors.Window;
            this.pictureBoxPolygonDraw.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.pictureBoxPolygonDraw.Location = new System.Drawing.Point(870, 242);
            this.pictureBoxPolygonDraw.Name = "pictureBoxPolygonDraw";
            this.pictureBoxPolygonDraw.Size = new System.Drawing.Size(501, 375);
            this.pictureBoxPolygonDraw.TabIndex = 6;
            this.pictureBoxPolygonDraw.TabStop = false;
            // 
            // btnUploadTQS
            // 
            this.btnUploadTQS.Font = new System.Drawing.Font("Segoe UI", 15.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnUploadTQS.Location = new System.Drawing.Point(55, 431);
            this.btnUploadTQS.Margin = new System.Windows.Forms.Padding(4);
            this.btnUploadTQS.Name = "btnUploadTQS";
            this.btnUploadTQS.Size = new System.Drawing.Size(321, 132);
            this.btnUploadTQS.TabIndex = 7;
            this.btnUploadTQS.Text = "Upload TQS Comparison JSON";
            this.btnUploadTQS.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1425, 648);
            this.Controls.Add(this.btnUploadTQS);
            this.Controls.Add(this.pictureBoxPolygonDraw);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.rtbStatus);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnUpload);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 9.75F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.Margin = new System.Windows.Forms.Padding(4);
            this.Name = "Form1";
            this.Text = "DXF Reader Application";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxPolygonDraw)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnUpload;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.RichTextBox rtbStatus;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.PictureBox pictureBoxPolygonDraw;
        private System.Windows.Forms.Button btnUploadTQS;
    }
}

