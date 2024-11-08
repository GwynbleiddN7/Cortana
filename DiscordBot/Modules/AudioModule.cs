﻿using CliWrap;
using Discord.Audio;
using Discord.Interactions;
using Discord.WebSocket;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos.Streams;
using Discord;
using Processor;
using YoutubeExplode.Search;
using YoutubeExplode.Videos;

namespace DiscordBot.Modules
{
    public static class AudioHandler
    {
        public struct ChannelClient
        {
            public SocketVoiceChannel VoiceChannel { get; }
            public IAudioClient? AudioClient { get; } = null;
            public AudioOutStream? AudioStream { get; } = null;

            public ChannelClient(SocketVoiceChannel newVoiceChannel, IAudioClient? newAudioClient = null, AudioOutStream? newAudioStream = null)
            {
                VoiceChannel = newVoiceChannel;
                AudioClient = newAudioClient;
                AudioStream = newAudioStream;
            }
        }

        private readonly struct QueueStructure(CancellationTokenSource newToken, MemoryStream newStream, ulong newGuildId)
        {
            public CancellationTokenSource Token { get; } = newToken;
            public MemoryStream Data { get; } = newStream;
            public ulong GuildId { get; } = newGuildId;
        }

        private readonly struct JoinStructure(CancellationTokenSource newToken, Func<Task> newTask)
        {
            public CancellationTokenSource Token { get; } = newToken;
            public Func<Task> Task { get; } = newTask;
        }
        
        public static readonly Dictionary<ulong, ChannelClient> AudioClients = new();
        private static readonly Dictionary<ulong, List<QueueStructure>> AudioQueue = new();
        private static readonly Dictionary<ulong, List<JoinStructure>> JoinQueue = new();
        
        //---------------------------- Audio Functions ----------------------------------------------

        private static async Task<MemoryStream> ExecuteFfmpeg(Stream? videoStream = null, string filePath = "")
        {
            var memoryStream = new MemoryStream();
            await Cli.Wrap("ffmpeg")
                .WithArguments($" -hide_banner -loglevel debug -i {(videoStream != null ? "pipe:0" : $"\"{filePath}\"")} -ac 2 -f s16le -ar 48000 pipe:1")
                .WithStandardInputPipe((videoStream != null ? PipeSource.FromStream(videoStream) : PipeSource.Null))
                .WithStandardOutputPipe(PipeTarget.ToStream(memoryStream))
                .ExecuteAsync();
            return memoryStream;
        }

        public static async Task<Stream> GetYoutubeAudioStream(string url)
        {
            var youtube = new YoutubeClient();
            StreamManifest streamManifest = await youtube.Videos.Streams.GetManifestAsync(url);
            IStreamInfo streamInfo = streamManifest.GetAudioOnlyStreams().GetWithHighestBitrate();
            Stream stream = await youtube.Videos.Streams.GetAsync(streamInfo);
            return stream;
        }

        public static async Task<Video> GetYoutubeVideoInfos(string url)
        {
            var youtube = new YoutubeClient();

            string link = url.Split("&").First();
            var substrings = new[] { "https://www.youtube.com/watch?v=", "https://youtu.be/" };
            string? result = null;
            foreach (string sub in substrings)
            {
                if (link.StartsWith(sub)) result = link.Substring(sub.Length);
            }

            if (result != null) return await youtube.Videos.GetAsync(result);
            IReadOnlyList<VideoSearchResult> videos = await youtube.Search.GetVideosAsync(url).CollectAsync(1);
            result = videos.First().Id;
            return await youtube.Videos.GetAsync(result);
        }

        public static async Task<bool> Play(string audio, ulong guildId, EAudioSource path)
        {
            if (!AudioClients.ContainsKey(guildId) || (AudioClients.ContainsKey(guildId) && AudioClients[guildId].AudioStream == null)) return false;

            var memoryStream = new MemoryStream();
            switch (path)
            {
                case EAudioSource.Youtube:
                {
                    Stream stream = await GetYoutubeAudioStream(audio);
                    memoryStream = await ExecuteFfmpeg(videoStream: stream);
                    break;
                }
                case EAudioSource.Local:
                    audio = $"Storage/Sound/{audio}.mp3";
                    memoryStream = await ExecuteFfmpeg(filePath: audio);
                    break;
            }

            var audioQueueItem = new QueueStructure(new CancellationTokenSource(), memoryStream, guildId);
            if (AudioQueue.TryGetValue(guildId, out List<QueueStructure>? queue)) queue.Add(audioQueueItem);
            else AudioQueue.Add(guildId, [audioQueueItem]);

            if (AudioQueue[guildId].Count == 1) NextAudioQueue(guildId);
            
            return true;
        }

        private static void NextAudioQueue(ulong guildId)
        {
            Task audioTask = Task.Run(() => SendBuffer(AudioQueue[guildId][0]));
            audioTask.ContinueWith((_) =>
            {
                AudioQueue[guildId][0].Token.Dispose();
                AudioQueue[guildId][0].Data.Dispose();
                AudioQueue[guildId].RemoveAt(0);
                if (AudioQueue[guildId].Count > 0) NextAudioQueue(guildId);
            });
        }  

        private static async Task SendBuffer(QueueStructure item)
        {
            try
            {
                await AudioClients[item.GuildId].AudioStream!.WriteAsync(item.Data.GetBuffer(), item.Token.Token);
            }
            finally
            {
                await AudioClients[item.GuildId].AudioStream!.FlushAsync();
            }
        }

        public static string Skip(ulong guildId)
        {
            if (!AudioQueue.TryGetValue(guildId, out List<QueueStructure>? audioQueue)) return "Non c'è niente da skippare";
            if (audioQueue.Count <= 0) return "Non c'è niente da skippare";
            audioQueue[0].Token.Cancel();
            return "Audio skippato";
        }

        public static string Clear(ulong guildId)
        {
            if (AudioQueue.ContainsKey(guildId) && AudioQueue[guildId].Count > 0)
            {
                for(int i = AudioQueue[guildId].Count - 1; i >= 0; i--)
                {
                    if(i == 0)
                    {
                        AudioQueue[guildId][i].Token.Cancel();
                        continue;
                    }

                    AudioQueue[guildId][i].Token.Cancel();
                    AudioQueue[guildId][i].Token.Dispose();

                    AudioQueue[guildId][i].Data.Dispose();

                    AudioQueue[guildId].RemoveAt(i);
                }
            
                AudioQueue[guildId].Clear();
                return "Queue rimossa";
            }
            return "Non c'è niente in coda";
        }

        //-------------------------------------------------------------------------------------------

        //------------------------ Channels Checking Function ---------------------------------------

        private static bool ShouldCortanaStay(SocketGuild guild)
        {
            SocketVoiceChannel? cortanaChannel = GetCurrentCortanaChannel(guild);
            List<SocketVoiceChannel> availableChannels = GetAvailableChannels(guild);

            if (availableChannels.Count <= 0) return cortanaChannel == null;
            return cortanaChannel != null && availableChannels.Contains(cortanaChannel);
        }

        private static bool IsChannelAvailable(SocketVoiceChannel channel)
        {
            if (channel.Id == DiscordData.GuildSettings[channel.Guild.Id].AFKChannel) return false;
            if (channel.ConnectedUsers.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID)) return channel.ConnectedUsers.Count > 1;
            return channel.ConnectedUsers.Count > 0;
        }

        public static SocketVoiceChannel? GetAvailableChannel(SocketGuild guild)
        {
            return guild.VoiceChannels.FirstOrDefault(IsChannelAvailable);
        }

        private static List<SocketVoiceChannel> GetAvailableChannels(SocketGuild guild)
        {
            List<SocketVoiceChannel> channels = [];
            channels.AddRange(guild.VoiceChannels.Where(IsChannelAvailable));
            return channels;
        }

        public static SocketVoiceChannel? GetCurrentCortanaChannel(SocketGuild guild)
        {
            return guild.VoiceChannels.FirstOrDefault(voiceChannel => voiceChannel.ConnectedUsers.Select(x => x.Id).Contains(DiscordData.DiscordIDs.CortanaID));
        }

        private static bool IsConnected(SocketVoiceChannel voiceChannel, SocketGuild guild)
        {
            return AudioClients.TryGetValue(guild.Id, out ChannelClient client) && client.VoiceChannel == voiceChannel && GetCurrentCortanaChannel(guild) == voiceChannel;
        }

        //-------------------------------------------------------------------------------------------

        //-------------------------- Connection Functions -------------------------------------------

        public static void HandleConnection(SocketGuild guild)
        {
            if (!ShouldCortanaStay(guild))
            {
                if (DiscordData.GuildSettings[guild.Id].AutoJoin)
                {
                    SocketVoiceChannel? channel = GetAvailableChannel(guild);
                    if (channel == null) Disconnect(guild.Id);
                    else Connect(channel);
                }
                else Disconnect(guild.Id);
            }
            else EnsureChannel(GetCurrentCortanaChannel(guild));
        }

        private static void AddToJoinQueue(Func<Task> taskToAdd, ulong guildId)
        {
            var queueItem = new JoinStructure(new CancellationTokenSource(), taskToAdd);
            if (JoinQueue.TryGetValue(guildId, out List<JoinStructure>? joinStructures)) joinStructures.Add(queueItem);
            else JoinQueue.Add(guildId, [queueItem]);

            if (JoinQueue[guildId].Count == 1) NextJoinQueue(guildId);
        }

        private static async void NextJoinQueue(ulong guildId)
        {
            await JoinQueue[guildId][0].Task();

            JoinQueue[guildId][0].Token.Dispose();
            JoinQueue[guildId].RemoveAt(0);
            if (JoinQueue[guildId].Count <= 0) return;
            JoinQueue[guildId] = [JoinQueue[guildId].Last()];

            NextJoinQueue(guildId);
        }

        private static async Task Join(SocketVoiceChannel voiceChannel)
        {
            SocketGuild guild = voiceChannel.Guild;

            try
            {
                await Task.Delay(1500);

                if (!GetAvailableChannels(voiceChannel.Guild).Contains(voiceChannel)) return;
                else if (IsConnected(voiceChannel, guild)) return;

                Clear(guild.Id);
                DisposeConnection(guild.Id);

                var newPair = new ChannelClient(voiceChannel);
                AudioClients.Add(guild.Id, newPair);

                IAudioClient? audioClient = await voiceChannel.ConnectAsync();
                AudioOutStream? streamOut = audioClient.CreatePCMStream(AudioApplication.Mixed, 64000, packetLoss: 0);
                AudioClients[guild.Id] = new ChannelClient(voiceChannel, audioClient, streamOut);

                await Play("Hello", guild.Id, EAudioSource.Local);
            }
            catch
            {
                DiscordData.SendToChannel("C'è stato un errore nel Join del canale vocale", ECortanaChannels.Log);
            }
        }

        private static async Task Leave(SocketVoiceChannel voiceChannel)
        {
            SocketGuild guild = voiceChannel.Guild;

            try
            {
                Clear(guild.Id);
                DisposeConnection(guild.Id);
                if (voiceChannel == GetCurrentCortanaChannel(guild)) await voiceChannel.DisconnectAsync();
            }
            catch 
            {
                DiscordData.SendToChannel("C'è stato un errore nel Join del canale vocale", ECortanaChannels.Log);
            }
        }

        private static void DisposeConnection(ulong guildId)
        {
            if (AudioClients.ContainsKey(guildId))
            {
                AudioClients[guildId].AudioStream?.Dispose();
                AudioClients[guildId].AudioClient?.Dispose();
                AudioClients.Remove(guildId);
            }
        }

        public static string Connect(SocketVoiceChannel channel)
        {
            if (GetCurrentCortanaChannel(channel.Guild) == channel) return "Sono già qui";
            AddToJoinQueue(() => Join(channel), channel.Guild.Id);

            return "Arrivo";
        }

        public static string Disconnect(ulong guildId)
        {
            foreach ((ulong clientId, ChannelClient clientChannel) in AudioClients)
            {
                if (clientId != guildId) continue;
                AddToJoinQueue(() => Leave(clientChannel.VoiceChannel), guildId);

                return "Mi sto disconnettendo";
            }
            return "Non sono connessa a nessun canale";
        }

        private static void EnsureChannel(SocketVoiceChannel? channel)
        {
            if (channel == null) return;
            if (IsConnected(channel, channel.Guild)) return;
            AddToJoinQueue(() => Join(channel), channel.Guild.Id);
        }

        //-------------------------------------------------------------------------------------------
    }

    [Group("media", "Gestione audio")]
    public class AudioModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("meme", "Metto un meme tra quelli disponibili")]
        public async Task Meme([Summary("nome", "Nome del meme")] string name, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.Si)
        {
            await DeferAsync(ephemeral: ephemeral == EAnswer.Si);

            foreach((string title, MemeJsonStructure memeStruct) in DiscordData.Memes)
            {
                if (!memeStruct.Alias.Contains(name.ToLower())) continue;
                string link = memeStruct.Link;

                Video result = await AudioHandler.GetYoutubeVideoInfos(link);
                TimeSpan duration = result.Duration ?? TimeSpan.Zero;
                Embed embed = DiscordData.CreateEmbed(title, description: $@"{duration:hh\:mm\:ss}");
                embed = embed.ToEmbedBuilder()
                    .WithUrl(result.Url)
                    .WithThumbnailUrl(result.Thumbnails[^1].Url)
                    .Build();

                await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

                bool status = await AudioHandler.Play(result.Url, Context.Guild.Id, EAudioSource.Youtube);
                if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso mandare il meme");
                return;
            }
            await FollowupAsync("Non ho nessun meme salvato con quel nome", ephemeral: ephemeral == EAnswer.Si);
        }

        [SlashCommand("metti", "Metti qualcosa da youtube", runMode: RunMode.Async)]
        public async Task Play([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            await DeferAsync(ephemeral: ephemeral == EAnswer.Si);

            Video result = await AudioHandler.GetYoutubeVideoInfos(text);
            TimeSpan duration = result.Duration ?? TimeSpan.Zero;
            Embed embed = DiscordData.CreateEmbed(result.Title, description: $"{duration:hh\\:mm\\:ss}");
            embed = embed.ToEmbedBuilder()
                .WithUrl(result.Url)
                .WithThumbnailUrl(result.Thumbnails[^1].Url)
                .Build();

            await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

            bool status = await AudioHandler.Play(result.Url, Context.Guild.Id, EAudioSource.Youtube);
            if (!status) await Context.Channel.SendMessageAsync("Non sono connessa a nessun canale, non posso mandare il video");
        }

        [SlashCommand("skippa", "Skippa quello che sto dicendo")]
        public async Task Skip([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            string result = AudioHandler.Skip(Context.Guild.Id);
            await RespondAsync(result, ephemeral: ephemeral == EAnswer.Si);
        }

        [SlashCommand("ferma", "Rimuovi tutto quello che c'è in coda")]
        public async Task Clear([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            string result = AudioHandler.Clear(Context.Guild.Id);
            await RespondAsync(result, ephemeral: ephemeral == EAnswer.Si);
        }

        [SlashCommand("connetti", "Entro nel canale dove sono stata chiamata")]
        public async Task Join([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            var text = "Non posso connettermi se non sei in un canale";
            foreach(SocketVoiceChannel? voiceChannel in Context.Guild.VoiceChannels)
            {
                if (!voiceChannel.ConnectedUsers.Contains(Context.User)) continue;
                text = AudioHandler.Connect(voiceChannel);
                break;
            }
            await RespondAsync(text, ephemeral: ephemeral == EAnswer.Si);
        }

        [SlashCommand("disconnetti", "Esco dal canale vocale")]
        public async Task Disconnect([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            string text = AudioHandler.Disconnect(Context.Guild.Id);
            await RespondAsync(text, ephemeral: ephemeral == EAnswer.Si);
        }

        [SlashCommand("elenco-meme", "Lista dei meme disponibili")]
        public async Task GetMemes([Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.Si)
        {
            Embed embed = DiscordData.CreateEmbed("Memes");
            var tempEmbed = embed.ToEmbedBuilder();
            foreach (EMemeCategory category in Enum.GetValues(typeof(EMemeCategory)))
            {
                string categoryString = DiscordData.Memes.Where(meme => meme.Value.Category == category).Aggregate("", (current, meme) => current + $"[{meme.Key}]({meme.Value.Link})\n");
                if (categoryString.Length == 0) continue;
                tempEmbed.AddField(category.ToString(), categoryString);
            }
            await RespondAsync(embed: tempEmbed.Build(), ephemeral: ephemeral == EAnswer.Si);
        }

        [SlashCommand("scarica-musica", "Scarica una canzone da youtube", runMode: RunMode.Async)]
        public async Task DownloadMusic([Summary("video", "Link o nome del video youtube")] string text, [Summary("ephemeral", "Vuoi vederlo solo tu?")] EAnswer ephemeral = EAnswer.No)
        {
            await DeferAsync(ephemeral: ephemeral == EAnswer.Si);

            Video result = await AudioHandler.GetYoutubeVideoInfos(text);
            TimeSpan duration = result.Duration ?? TimeSpan.Zero;
            Embed embed = DiscordData.CreateEmbed(result.Title, description: $"{duration:hh\\:mm\\:ss}");
            embed = embed.ToEmbedBuilder()
            .WithDescription("Musica in download...")
            .WithUrl(result.Url)
            .WithThumbnailUrl(result.Thumbnails[^1].Url)
            .Build();

            await FollowupAsync(embed: embed, ephemeral: ephemeral == EAnswer.Si);

            Stream stream = await AudioHandler.GetYoutubeAudioStream(result.Url);
            await Context.Channel.SendFileAsync(stream, result.Title + ".mp3");
        }
    }
}
