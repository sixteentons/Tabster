﻿#region

using System.Net;
using System.Text;
using System.Windows.Forms;

#endregion

namespace Tabster
{
    public class TabsterWebClient : WebClient
    {
        public TabsterWebClient(IWebProxy proxy = null)
        {
            Proxy = proxy;
            Encoding = Encoding.UTF8;
            Headers.Add("user-agent", string.Format("Tabster {0}", Application.ProductVersion));
        }
    }
}