﻿#region

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Windows.Forms;
using Tabster.Utilities.Extensions;
using Tabster.Utilities.Reflection;

#endregion

namespace Tabster.Forms
{
    internal partial class SplashScreen : Form
    {
        private readonly bool _safeMode;

        public SplashScreen()
        {
            InitializeComponent();

#if PORTABLE
            lblPortable.Visible = true;
#endif

            if (_safeMode)
                lblSafeMode.Visible = true;

            RoundBorderForm(this);

            lblProgress.Text = string.Empty;

            lblVersion.Text = string.Format("v{0}", new Version(Application.ProductVersion).ToShortString());
            lblCopyright.Text = AssemblyUtilities.GetCopyrightString(Assembly.GetExecutingAssembly());
            lblVersion.ForeColor = Color.Gray;
            BringToFront();
        }

        public SplashScreen(bool safeMode) : this()
        {
            _safeMode = safeMode;
        }

        public static void RoundBorderForm(Form frm)
        {
            var bounds = new Rectangle(0, 0, frm.Width, frm.Height);
            const int cornerRadius = 18;
            var path = new GraphicsPath();
            path.AddArc(bounds.X, bounds.Y, cornerRadius, cornerRadius, 180, 90);
            path.AddArc(bounds.X + bounds.Width - cornerRadius, bounds.Y, cornerRadius, cornerRadius, 270, 90);
            path.AddArc(bounds.X + bounds.Width - cornerRadius, bounds.Y + bounds.Height - cornerRadius, cornerRadius, cornerRadius, 0, 90);
            path.AddArc(bounds.X, bounds.Y + bounds.Height - cornerRadius, cornerRadius, cornerRadius, 90, 90);
            path.CloseAllFigures();

            frm.Region = new Region(path);
        }

        public void SetStatus(string status)
        {
            if (lblProgress.InvokeRequired)
            {
                var d = new SetStatusCallback(SetStatus);
                Invoke(d, new object[] {status});
            }

            else
            {
                lblProgress.Text = status;
            }
        }

        private delegate void SetStatusCallback(string text);
    }
}