namespace FileManagement
{
    partial class FormProfile
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
            this.labelProfile = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // labelProfile
            // 
            this.labelProfile.Location = new System.Drawing.Point(12, 9);
            this.labelProfile.Name = "labelProfile";
            this.labelProfile.Size = new System.Drawing.Size(616, 681);
            this.labelProfile.TabIndex = 0;
            // 
            // FormProfile
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(12F, 24F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(640, 699);
            this.Controls.Add(this.labelProfile);
            this.KeyPreview = true;
            this.Name = "FormProfile";
            this.Text = "FormProfile";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.FormProfile_FormClosing);
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.FormProfile_KeyDown);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label labelProfile;
    }
}