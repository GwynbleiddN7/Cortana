﻿using Discord;
using Discord.Interactions;
using Processor;

namespace DiscordBot.Modules
{
    [Group("domotica", "Gestione domotica")]
    [RequireOwner]
    public class AutomationModule : InteractionModuleBase<SocketInteractionContext>
    {

        [SlashCommand("lamp", "Accendi o spegni la luce")]
        public async Task LightToggle()
        {
            string result = Hardware.SwitchLamp(ETrigger.Toggle);
            Embed embed = DiscordData.CreateEmbed(title: result);
            await RespondAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("hardware", "Interagisci con l'hardware in camera", runMode: RunMode.Async)]
        public async Task HardwareInteract([Summary("dispositivo", "Con cosa vuoi interagire?")] EGpio element, [Summary("azione", "Cosa vuoi fare?")] ETrigger trigger)
        {
            await DeferAsync(ephemeral: true);

            string result = Hardware.SwitchFromEnum(element, trigger);

            Embed embed = DiscordData.CreateEmbed(title: result);
            await FollowupAsync(embed: embed, ephemeral: true);
        }

        [SlashCommand("room", "Accendi o spegni tutti i dispositivi", runMode: RunMode.Async)]
        public async Task BootUp([Summary("azione", "Cosa vuoi fare?")] ETrigger trigger)
        {
            Embed embed = DiscordData.CreateEmbed(title: "Procedo");
            await RespondAsync(embed: embed, ephemeral: true);

            Hardware.SwitchRoom(trigger);
        }

        [SlashCommand("ip", "Ti dico il mio IP pubblico", runMode: RunMode.Async)]
        public async Task GetIp()
        {
            await DeferAsync(ephemeral: true);
            string ip = await Hardware.GetPublicIp();
            await FollowupAsync($"L'IP pubblico è {ip}", ephemeral: true);
        }
    }
}