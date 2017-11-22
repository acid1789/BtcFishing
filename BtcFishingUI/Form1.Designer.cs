namespace BtcFishingUI
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
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this.lblConnections = new System.Windows.Forms.Label();
            this.tbOutput = new System.Windows.Forms.TextBox();
            this.timer1 = new System.Windows.Forms.Timer(this.components);
            this.lblBlocks = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.lblPotentials = new System.Windows.Forms.Label();
            this.lblBlockHeaders = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(12, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(69, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Connections:";
            // 
            // lblConnections
            // 
            this.lblConnections.AutoSize = true;
            this.lblConnections.Location = new System.Drawing.Point(83, 9);
            this.lblConnections.Name = "lblConnections";
            this.lblConnections.Size = new System.Drawing.Size(13, 13);
            this.lblConnections.TabIndex = 1;
            this.lblConnections.Text = "0";
            // 
            // tbOutput
            // 
            this.tbOutput.Location = new System.Drawing.Point(12, 25);
            this.tbOutput.Multiline = true;
            this.tbOutput.Name = "tbOutput";
            this.tbOutput.ReadOnly = true;
            this.tbOutput.Size = new System.Drawing.Size(838, 485);
            this.tbOutput.TabIndex = 2;
            // 
            // timer1
            // 
            this.timer1.Enabled = true;
            this.timer1.Tick += new System.EventHandler(this.timer1_Tick);
            // 
            // lblBlocks
            // 
            this.lblBlocks.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblBlocks.Location = new System.Drawing.Point(703, 9);
            this.lblBlocks.Name = "lblBlocks";
            this.lblBlocks.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.lblBlocks.Size = new System.Drawing.Size(147, 13);
            this.lblBlocks.TabIndex = 3;
            this.lblBlocks.Text = "blocks 2323/23";
            this.lblBlocks.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(118, 9);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(81, 13);
            this.label2.TabIndex = 4;
            this.label2.Text = "Potential Peers:";
            // 
            // lblPotentials
            // 
            this.lblPotentials.AutoSize = true;
            this.lblPotentials.Location = new System.Drawing.Point(205, 9);
            this.lblPotentials.Name = "lblPotentials";
            this.lblPotentials.Size = new System.Drawing.Size(63, 13);
            this.lblPotentials.TabIndex = 5;
            this.lblPotentials.Text = "lblPotentials";
            // 
            // lblBlockHeaders
            // 
            this.lblBlockHeaders.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.lblBlockHeaders.Location = new System.Drawing.Point(550, 9);
            this.lblBlockHeaders.Name = "lblBlockHeaders";
            this.lblBlockHeaders.RightToLeft = System.Windows.Forms.RightToLeft.Yes;
            this.lblBlockHeaders.Size = new System.Drawing.Size(147, 13);
            this.lblBlockHeaders.TabIndex = 6;
            this.lblBlockHeaders.Text = "blocks 2323/23";
            this.lblBlockHeaders.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(862, 522);
            this.Controls.Add(this.lblBlockHeaders);
            this.Controls.Add(this.lblPotentials);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.lblBlocks);
            this.Controls.Add(this.tbOutput);
            this.Controls.Add(this.lblConnections);
            this.Controls.Add(this.label1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label lblConnections;
        private System.Windows.Forms.TextBox tbOutput;
        private System.Windows.Forms.Timer timer1;
        private System.Windows.Forms.Label lblBlocks;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label lblPotentials;
        private System.Windows.Forms.Label lblBlockHeaders;
    }
}

