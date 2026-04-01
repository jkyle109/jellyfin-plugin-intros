using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.LocalIntros.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace Jellyfin.Plugin.LocalIntros;

public class IntroSessionManager : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly ILogger<IntroSessionManager> _logger;
    private readonly IntroProvider _introProvider;
    
    private readonly ConcurrentDictionary<string, Guid> _sessionLastPlayedItemIds = new ConcurrentDictionary<string, Guid>();

    public IntroSessionManager(ISessionManager sessionManager, ILoggerFactory loggerFactory)
    {
        _sessionManager = sessionManager;
        _logger = loggerFactory.CreateLogger<IntroSessionManager>();
        _introProvider = new IntroProvider(loggerFactory);
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
    }

    private async void OnPlaybackStart(object sender, PlaybackProgressEventArgs e)
    {
        try
        {
            if (!LocalIntrosPlugin.Instance.Configuration.ForceIntros)
                return;

            if (e.Item == null || e.Session == null)
                return;

            var kind = e.Item.GetBaseItemKind();
            if (kind != Jellyfin.Data.Enums.BaseItemKind.Movie && kind != Jellyfin.Data.Enums.BaseItemKind.Episode)
                return;

            var localPath = LocalIntrosPlugin.Instance.Configuration.Local;
            if (!string.IsNullOrEmpty(localPath) && e.Item.Path != null && e.Item.Path.StartsWith(localPath, StringComparison.OrdinalIgnoreCase))
                return;

            if (e.Item.ProviderIds.ContainsKey("prerolls.video"))
                return;

            if (_sessionLastPlayedItemIds.TryGetValue(e.Session.Id, out var lastPlayedId) && lastPlayedId == e.Item.Id)
            {
                return;
            }

            var intros = _introProvider.Local(e.Item);
            var introInfo = intros.FirstOrDefault();

            if (introInfo != null && introInfo.ItemId.HasValue)
            {
                _logger.LogInformation("ForceIntros: Injecting intro {IntroId} before {ItemName} in session {SessionId}", introInfo.ItemId.Value, e.Item.Name, e.Session.Id);
                
                _sessionLastPlayedItemIds[e.Session.Id] = e.Item.Id;

                var playRequest = new PlayRequest
                {
                    PlayCommand = PlayCommand.PlayNow,
                    ItemIds = new Guid[] { introInfo.ItemId.Value, e.Item.Id },
                    ControllingUserId = e.Session.UserId
                };

                await _sessionManager.SendPlayCommand(e.Session.Id, e.Session.Id, playRequest, CancellationToken.None).ConfigureAwait(false);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during ForceIntros injection");
        }
    }
}
