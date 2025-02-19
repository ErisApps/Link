﻿using CatCore;
using CatCore.Models.Shared;
using CatCore.Models.Twitch;
using CatCore.Models.Twitch.IRC;
using CatCore.Services.Interfaces;
using CatCore.Services.Twitch.Interfaces;
using SiraUtil.Logging;
using System;
using System.Linq;
using Zenject;

namespace Link
{
    internal class LinkBroadcaster : IInitializable, IDisposable
    {
        private readonly SiraLog _siraLog;
        private readonly ISongLinkManager _songLinkManager;
        private readonly CatCoreInstance _chatCoreInstance;
        private readonly IBeatmapStateManager _beatmapStateManager;
        private ITwitchService? _chatService;

        public LinkBroadcaster(SiraLog siraLog, ISongLinkManager songLinkManager, IBeatmapStateManager beatmapStateManager)
        {
            _siraLog = siraLog;
            _songLinkManager = songLinkManager;
            _beatmapStateManager = beatmapStateManager;
            _chatCoreInstance = CatCoreInstance.Create();
        }

        public void Initialize()
        {
            _chatService = _chatCoreInstance.RunTwitchServices();
            _chatService.OnTextMessageReceived += ChatService_OnTextMessageReceived;
        }

        private async void ChatService_OnTextMessageReceived(ITwitchService service, TwitchMessage msg)
        {
            if (msg.Message.ToLower().StartsWith("!link"))
            {
                _siraLog.Info("Detected a link command, attempting to fetch a map key.");
                if (_beatmapStateManager.ActiveBeatmap == null && _beatmapStateManager.LastBeatmap == null)
                {
                    _siraLog.Info("No beatmap has been played recently. Ignoring command.");
                    return;
                }
                try
                {
                    if (_beatmapStateManager.ActiveBeatmap == null)
                    {
                        _siraLog.Info("The player is not actively playing a map. Let's use the last beatmap they played.");
                        string? link = await _songLinkManager.GetSongLink(_beatmapStateManager.LastBeatmap!);
                        msg.Channel.SendMessage(Format(msg, link is null ? "Could not find a link for the last played map." : $"The most recently played map was {link}"));
                    }
                    else
                    {
                        _siraLog.Info("The player is actively playing a map. Trying to find it's link.");
                        string? link = await _songLinkManager.GetSongLink(_beatmapStateManager.ActiveBeatmap!);
                        msg.Channel.SendMessage(Format(msg, link is null ? "Could not find a link for the current map." : $"The currently played map is {link}"));
                    }
                }
                catch (Exception e)
                {
                    _siraLog.Error("An error occurred while trying to fetch the beatmap link.");
                    _siraLog.Error(e);
                }
            }
        }

        private string Format(TwitchMessage msg, string message) => $"! {msg.Sender.DisplayName}, {message}";
        
        public void Dispose()
        {
            if (_chatService != null)
                _chatService.OnTextMessageReceived -= ChatService_OnTextMessageReceived;

            _chatCoreInstance.StopAllServices();
        }
    }
}