﻿// Copyright © 2014 onwards, Andrew Whewell
// All rights reserved.
//
// Redistribution and use of this software in source and binary forms, with or without modification, are permitted provided that the following conditions are met:
//    * Redistributions of source code must retain the above copyright notice, this list of conditions and the following disclaimer.
//    * Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following disclaimer in the documentation and/or other materials provided with the distribution.
//    * Neither the name of the author nor the names of the program's contributors may be used to endorse or promote products derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE AUTHORS OF THE SOFTWARE BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using InterfaceFactory;
using VirtualRadar.Interface;
using System.ComponentModel;
using VirtualRadar.Interface.WebSite;
using VirtualRadar.Interface.WebServer;
using VirtualRadar.Localisation;
using VirtualRadar.Interface.View;
using VirtualRadar.Plugin.WebAdmin.View;

namespace VirtualRadar.Plugin.WebAdmin
{
    /// <summary>
    /// Implements <see cref="IPlugin"/> to tell VRS about our plugin.
    /// </summary>
    public class Plugin : IPlugin
    {
        /// <summary>
        /// The options that govern the plugin's behaviour.
        /// </summary>
        private Options _Options = new Options();

        /// <summary>
        /// The folder that has all of the views.
        /// </summary>
        private string ProtectedFolder = "WebAdmin";

        /// <summary>
        /// The object that we use to extend the website.
        /// </summary>
        private IWebSiteExtender _WebSiteExtender;

        /// <summary>
        /// The implementation of <see cref="IWebAdminViewManager"/> that we register with the system,
        /// overriding the stub version in VirtualRadar.WebSite.
        /// </summary>
        private WebAdminViewManager _WebAdminViewManager;

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Id { get { return "VirtualRadar.Plugin.WebAdmin"; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string PluginFolder { get; set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Name { get { return WebAdminStrings.PluginName; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public bool HasOptions { get { return true; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Version { get { return "2.4"; } }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string Status { get; private set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public string StatusDescription { get; private set; }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public event EventHandler StatusChanged;

        /// <summary>
        /// Raises <see cref="StatusChanged"/>.
        /// </summary>
        /// <param name="args"></param>
        protected virtual void OnStatusChanged(EventArgs args)
        {
            EventHelper.Raise(StatusChanged, this, args);
        }

        /// <summary>
        /// Creates a new object.
        /// </summary>
        public Plugin()
        {
            Status = WebAdminStrings.Disabled;
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="classFactory"></param>
        public void RegisterImplementations(IClassFactory classFactory)
        {
            _WebAdminViewManager = (WebAdminViewManager)(new WebAdminViewManager().Singleton);
            _WebAdminViewManager.Initialise(ProtectedFolder);
            _WebAdminViewManager.RegisterTemplateFileName("@head.html@", Path.GetFullPath(Path.Combine(PluginFolder, "web/webadmin/templates/head.html")));

            classFactory.Register<IWebAdminViewManager, WebAdminViewManager>();
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        /// <param name="parameters"></param>
        public void Startup(PluginStartupParameters parameters)
        {
            _Options = OptionsStorage.Load(this);

            var pathFromRoot = String.Format("/{0}/", ProtectedFolder);
            _WebAdminViewManager.Startup(parameters.WebSite);
            _WebAdminViewManager.RegisterTranslations(typeof(VirtualRadar.WebSite.WebSiteStrings), "", false);
            _WebAdminViewManager.RegisterTranslations(typeof(VirtualRadar.Localisation.Strings), "Server", false);
            _WebAdminViewManager.RegisterTranslations(typeof(WebAdminStrings), "WebAdmin", true);

            _WebAdminViewManager.AddWebAdminView(new WebAdminView(pathFromRoot, "About.html", () => new View.AboutView(), typeof(WebAdminStrings)));
            _WebAdminViewManager.AddWebAdminView(new WebAdminView(pathFromRoot, "Log.html", () => new View.LogView(), typeof(WebAdminStrings)));
            _WebAdminViewManager.AddWebAdminView(new WebAdminView(pathFromRoot, "Index.html", () => new View.MainView(parameters.UPnpManager, parameters.FlightSimulatorAircraftList), typeof(WebAdminStrings)));
            _WebAdminViewManager.RegisterWebAdminViewFolder(PluginFolder, "Web");

            _WebSiteExtender = Factory.Singleton.Resolve<IWebSiteExtender>();
            _WebSiteExtender.Enabled = _Options.Enabled;
            _WebSiteExtender.Initialise(parameters);
            _WebSiteExtender.ProtectFolder(ProtectedFolder);

            ApplyOptions(_Options);
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public void GuiThreadStartup()
        {
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public void Shutdown()
        {
        }

        /// <summary>
        /// See interface docs.
        /// </summary>
        public void ShowWinFormsOptionsUI()
        {
            using(var dialog = new WinForms.OptionsView()) {
                var webServer = Factory.Singleton.Resolve<IAutoConfigWebServer>().Singleton.WebServer;
                dialog.IndexPageAddress = String.Format("{0}/{1}", webServer.LocalAddress, "WebAdmin/index.html");
                dialog.Options = OptionsStorage.Load(this);

                if(dialog.ShowDialog() == DialogResult.OK) {
                    OptionsStorage.Save(this, dialog.Options);
                    ApplyOptions(dialog.Options);
                }
            }
        }

        /// <summary>
        /// Applies the options.
        /// </summary>
        /// <param name="options"></param>
        private void ApplyOptions(Options options)
        {
            _Options = options;
            _WebSiteExtender.Enabled = _Options.Enabled;
            _WebSiteExtender.RestrictAccessToFolder(ProtectedFolder, _Options.Access);
            _WebAdminViewManager.Enabled = _Options.Enabled;

            using(new CultureSwitcher()) {
                if(!options.Enabled) {
                    Status = WebAdminStrings.Disabled;
                } else {
                    Status = Strings.Enabled;
                }
            }

            OnStatusChanged(EventArgs.Empty);
        }
    }
}
