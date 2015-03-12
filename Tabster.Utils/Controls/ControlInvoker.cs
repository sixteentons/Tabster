﻿#region

using System.Windows.Forms;

#endregion

namespace Tabster.Utilities.Controls
{
    public static class ControlInvoker
    {
        public static void InvokeIfRequired(this Control control, MethodInvoker action)
        {
            if (control.InvokeRequired)
            {
                control.Invoke(action);
            }
            else
            {
                action();
            }
        }
    }
}