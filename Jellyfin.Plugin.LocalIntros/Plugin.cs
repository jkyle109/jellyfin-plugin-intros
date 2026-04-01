using System;
using System.Collections.Generic;
using Jellyfin.Plugin.LocalIntros.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Library;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;

using MediaBrowser.Controller;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.LocalIntros
{
    public class LocalIntrosPlugin : BasePlugin<IntroPluginConfiguration>, IHasWebPages, IPluginServiceRegistrator
    {
        public override string Name => "Force Intros";

        public override Guid Id => Guid.Parse("B6FB4817-524D-4AD0-A067-8A66260FD432");

        public const int DefaultResolution = 1080;

        public static LocalIntrosPlugin Instance { get; private set; }

        public static ILibraryManager LibraryManager { get; private set; }

        public LocalIntrosPlugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer, ILibraryManager libraryManager)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            LibraryManager = libraryManager;
        }

        public IEnumerable<PluginPageInfo> GetPages()
        {
            yield return new PluginPageInfo
            {
                Name = Name,
                EmbeddedResourcePath = GetType().Namespace + ".Configuration.config.html"
            };
        }

        public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
        {
            serviceCollection.AddHostedService<IntroSessionManager>();
        }
    }
}
