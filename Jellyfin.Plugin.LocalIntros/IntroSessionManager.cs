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
        _logger.LogInformation("ForceIntros: IntroSessionManager is starting and hooking into PlaybackStart.");
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
            _logger.LogInformation("ForceIntros: PlaybackStart event triggered!");

            if (!LocalIntrosPlugin.Instance.Configuration.ForceIntros)
            {
                _logger.LogInformation("ForceIntros: Skipping because 'Force Intros' is disabled in configuration.");
                return;
            }

            if (e.Item == null || e.Session == null)
            {
                _logger.LogInformation("ForceIntros: Skipping because Item or Session is null.");
                return;
            }

            var kind = e.Item.GetBaseItemKind();
            _logger.LogInformation("ForceIntros: Item Kind is {Kind} for {ItemName}", kind, e.Item.Name);

            // Movies are natively handled by the standard IntroProvider since they don't typically have binge-queues
            // We only need to utilize the server-side ForceIntros override for continuous Episodes!
            if (kind != Jellyfin.Data.Enums.BaseItemKind.Episode)
                return;

            var localPath = LocalIntrosPlugin.Instance.Configuration.Local;
            if (!string.IsNullOrEmpty(localPath) && e.Item.Path != null && e.Item.Path.StartsWith(localPath, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("ForceIntros: Skipping because this item is physically located in the intro directory.");
                return;
            }

            if (e.Item.ProviderIds.ContainsKey("prerolls.video"))
            {
                _logger.LogInformation("ForceIntros: Skipping because this item possesses the prerolls.video provider ID.");
                return;
            }

            if (_sessionLastPlayedItemIds.TryGetValue(e.Session.Id, out var lastPlayedId) && lastPlayedId == e.Item.Id)
            {
                _logger.LogInformation("ForceIntros: Skipping because we just forced this exact item to play!");
                return;
            }

            var intros = _introProvider.Local(e.Item);
            var introInfo = intros.FirstOrDefault();

            if (introInfo != null && introInfo.ItemId.HasValue)
            {
                _logger.LogInformation("ForceIntros: Injecting intro {IntroId} before {ItemName} in session {SessionId}", introInfo.ItemId.Value, e.Item.Name, e.Session.Id);
                
                _sessionLastPlayedItemIds[e.Session.Id] = e.Item.Id;

                var newQueue = new System.Collections.Generic.List<Guid> { introInfo.ItemId.Value };
                bool foundCurrent = false;

                if (e.Session.NowPlayingQueue != null)
                {
                    foreach (var queuedItem in e.Session.NowPlayingQueue)
                    {
                        if (queuedItem.Id == e.Item.Id)
                        {
                            foundCurrent = true;
                        }

                        if (foundCurrent)
                        {
                            newQueue.Add(queuedItem.Id);
                        }
                    }
                }

                if (!foundCurrent)
                {
                    newQueue.Add(e.Item.Id);
                }

                _logger.LogInformation("ForceIntros: Reconstructed queue length: {Length} items.", newQueue.Count);

                var playRequest = new PlayRequest
                {
                    PlayCommand = PlayCommand.PlayNow,
                    ItemIds = newQueue.ToArray(),
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
