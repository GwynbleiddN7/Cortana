﻿using Microsoft.Extensions.Configuration;
using Telegram.Bot;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;

namespace TelegramBot
{
    static class HardwareEmoji
    {
        public const string LIGHT = "💡";
        public const string PC = "🖥";
        public const string PLUGS = "⚡";
        public const string MONITOR = "📺";
        public const string ON = "\U0001f7e9\U0001f7e9\U0001f7e9";
        public const string OFF = "\U0001f7e5\U0001f7e5\U0001f7e5";
    }

    public class TelegramBot
    {
        enum EAnswerCommands { QRCODE }
        private static Dictionary<long, EAnswerCommands> AnswerCommands;
        private static Dictionary<long, string> HardwareAction;
        private static List<long> HardwarePermissions;



        public static void BootTelegramBot() => new TelegramBot().Main();

        public void Main()
        {
            var config = ConfigurationBuilder();
            var cortana = new TelegramBotClient(config["token"]);
            cortana.StartReceiving(UpdateHandler, ErrorHandler);

            TelegramData.Init(cortana);
            AnswerCommands = new();
            HardwareAction = new();
            HardwarePermissions = new()
            {
                TelegramData.Data.ChiefID,
                TelegramData.Data.LF_ID
            };

            TelegramData.SendToUser(TelegramData.Data.ChiefID, "I'm Ready Chief!", false);
        }

        private Task UpdateHandler(ITelegramBotClient Cortana, Update update, CancellationToken cancellationToken)
        {
            switch (update.Type)
            {
                case UpdateType.CallbackQuery:
                    HandleCallback(Cortana, update);
                    break;
                case UpdateType.Message:
                    HandleMessage(Cortana, update);
                    break;
                default:
                    break;
            }

            return Task.CompletedTask;
        }
        

        private async void HandleMessage(ITelegramBotClient Cortana, Update update)
        {
            if (update.Message == null) return;

            var ChatID = update.Message.Chat.Id;
            var UserID = update.Message.From == null ? ChatID : update.Message.From.Id;
            if (update.Message.Type == MessageType.Text && update.Message.Text != null)
            {
                if (update.Message.Text.StartsWith("/"))
                {
                    var message = update.Message.Text.Substring(1).Split(" ").First();

                    switch (message)
                    {
                        case "ip":
                            var ip = await Utility.Functions.GetPublicIP();
                            await Cortana.SendTextMessageAsync(ChatID, $"IP: {ip}");
                            break;
                        case "temperatura":
                            var temp = Utility.HardwareDriver.GetCPUTemperature();
                            await Cortana.SendTextMessageAsync(ChatID, $"Temperatura: {temp}");
                            break;
                        case "qrcode":
                            if (AnswerCommands.ContainsKey(ChatID)) AnswerCommands.Remove(ChatID);
                            AnswerCommands.Add(ChatID, EAnswerCommands.QRCODE);
                            await Cortana.SendTextMessageAsync(ChatID, "Scrivi il contenuto");
                            break;
                        case "buy":
                            if(ChatID == TelegramData.Data.PSM_ID)
                            {
                                Shopping.Buy(UserID, update.Message.Text.Substring(1));
                            }
                            else await Cortana.SendTextMessageAsync(ChatID, "Comando riservato al gruppo");
                            break;
                        case "my-debts":
                            if(ChatID == TelegramData.Data.PSM_ID)
                            {
                                await Cortana.SendTextMessageAsync(ChatID, Shopping.GetDebts(UserID));
                            }
                            else await Cortana.SendTextMessageAsync(ChatID, "Comando riservato al gruppo");
                            break;
                        case "hardware":
                            if(HardwarePermissions.Contains(UserID))
                            {
                                var mex = await Cortana.SendTextMessageAsync(ChatID, "Hardware Keyboard", replyMarkup: CreateHardwareButtons());
                                HardwareAction.Add(mex.MessageId, "");
                            }
                            else
                                await Cortana.SendTextMessageAsync(ChatID, "Non hai l'autorizzazione per eseguire questo comando");      
                            break;
                        case "keyboard":
                            if (HardwarePermissions.Contains(UserID))
                                await Cortana.SendTextMessageAsync(ChatID, "Hardware Toggle Keyboard", replyMarkup: CreateHardwareToggles());
                            else 
                                await Cortana.SendTextMessageAsync(ChatID, "Non hai l'autorizzazione per eseguire questo comando");         
                            break;
                    }
                }
                else
                {
                    if (!AnswerCommands.ContainsKey(ChatID))
                    {
                        if (!HardwarePermissions.Contains(UserID)) return;
                        switch (update.Message.Text)
                        {
                            case HardwareEmoji.LIGHT:
                                Utility.HardwareDriver.SwitchLamp(EHardwareTrigger.Toggle);
                                await Cortana.DeleteMessageAsync(ChatID, update.Message.MessageId);
                                break;
                            case HardwareEmoji.PC:
                                Utility.HardwareDriver.SwitchPC(EHardwareTrigger.Toggle);
                                await Cortana.DeleteMessageAsync(ChatID, update.Message.MessageId);
                                break;
                            case HardwareEmoji.PLUGS:
                                Utility.HardwareDriver.SwitchOutlets(EHardwareTrigger.Toggle);
                                await Cortana.DeleteMessageAsync(ChatID, update.Message.MessageId);
                                break;
                            case HardwareEmoji.ON:
                                Utility.HardwareDriver.SwitchRoom(EHardwareTrigger.On);
                                await Cortana.DeleteMessageAsync(ChatID, update.Message.MessageId);
                                break;
                            case HardwareEmoji.OFF:
                                Utility.HardwareDriver.SwitchRoom(EHardwareTrigger.Off);
                                await Cortana.DeleteMessageAsync(ChatID, update.Message.MessageId);
                                break;
                            case HardwareEmoji.MONITOR:
                                Utility.HardwareDriver.SwitchOLED(EHardwareTrigger.Toggle);
                                await Cortana.DeleteMessageAsync(ChatID, update.Message.MessageId);
                                break;
                            default:
                                break;
                        }
                    }
                    else
                    {
                        switch (AnswerCommands[ChatID])
                        {
                            case EAnswerCommands.QRCODE:
                                var ImageStream = Utility.Functions.CreateQRCode(content: update.Message.Text, useNormalColors: false, useBorders: true);
                                ImageStream.Position = 0;
                                await Cortana.SendPhotoAsync(ChatID, new InputFileStream(ImageStream, "QRCODE.png"));
                                AnswerCommands.Remove(ChatID);
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }


        private async void HandleCallback(ITelegramBotClient Cortana, Update update)
        {
            if (update.CallbackQuery == null || update.CallbackQuery.Data == null || update.CallbackQuery.Message == null) return;
            if (!HardwarePermissions.Contains(update.CallbackQuery.From.Id))
            {
                await Cortana.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                return;
            }

            string data = update.CallbackQuery.Data;
            int message_id = update.CallbackQuery.Message.MessageId;

            if (HardwareAction.ContainsKey(message_id))
            {
                if (HardwareAction[message_id] == "")
                {
                    HardwareAction[message_id] = data;

                    InlineKeyboardMarkup Action = CreateOnOffButtons();
                    await Cortana.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                    await Cortana.EditMessageReplyMarkupAsync(update.CallbackQuery.Message.Chat.Id, message_id, Action);
                }
                else
                {
                    if (data == "back")
                    {
                        HardwareAction[message_id] = "";
                        await Cortana.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                        await Cortana.EditMessageReplyMarkupAsync(update.CallbackQuery.Message.Chat.Id, message_id, CreateHardwareButtons());
                        return;
                    }

                    EHardwareTrigger trigger = data switch
                    {
                        "on" => EHardwareTrigger.On,
                        "off" => EHardwareTrigger.Off,
                        "toggle" => EHardwareTrigger.Toggle,
                        _ => EHardwareTrigger.Off
                    };

                    string result = HardwareAction[message_id] switch
                    {
                        "lamp" => Utility.HardwareDriver.SwitchLamp(trigger),
                        "pc" => Utility.HardwareDriver.SwitchPC(trigger),
                        "outlets" => Utility.HardwareDriver.SwitchOutlets(trigger),
                        "oled" => Utility.HardwareDriver.SwitchOLED(trigger),
                        "room" => Utility.HardwareDriver.SwitchRoom(trigger),
                        _ => ""
                    };


                    HardwareAction[message_id] = "";
                    try
                    {
                        await Cortana.AnswerCallbackQueryAsync(update.CallbackQuery.Id, result);
                        await Cortana.EditMessageReplyMarkupAsync(update.CallbackQuery.Message.Chat.Id, message_id, CreateHardwareButtons());
                    }
                    catch
                    {
                        await Cortana.AnswerCallbackQueryAsync(update.CallbackQuery.Id);
                        var mex = await Cortana.SendTextMessageAsync(update.CallbackQuery.Message.Chat.Id, "Hardware Keyboard", replyMarkup: CreateHardwareButtons());
                        HardwareAction.Remove(message_id);
                        HardwareAction.Add(mex.MessageId, "");
                    }
                }
            }
        }

        private InlineKeyboardMarkup CreateHardwareButtons()
        {
            InlineKeyboardButton[][] Rows = new InlineKeyboardButton[5][];

            Rows[0] = new InlineKeyboardButton[1];
            Rows[0][0] = InlineKeyboardButton.WithCallbackData("Light", "lamp");
         
            Rows[1] = new InlineKeyboardButton[1];
            Rows[1][0] = InlineKeyboardButton.WithCallbackData("PC", "pc");

            Rows[2] = new InlineKeyboardButton[1];
            Rows[2][0] = InlineKeyboardButton.WithCallbackData("Plugs", "outlets");

            Rows[3] = new InlineKeyboardButton[1];
            Rows[3][0] = InlineKeyboardButton.WithCallbackData("OLED", "oled");

            Rows[4] = new InlineKeyboardButton[1];
            Rows[4][0] = InlineKeyboardButton.WithCallbackData("Room", "room");

            InlineKeyboardMarkup hardwareKeyboard = new InlineKeyboardMarkup(Rows);
            return hardwareKeyboard;
        }

        private InlineKeyboardMarkup CreateOnOffButtons()
        {
            InlineKeyboardButton[][] Rows = new InlineKeyboardButton[3][];

            Rows[0] = new InlineKeyboardButton[2];
            Rows[0][0] = InlineKeyboardButton.WithCallbackData("On", "on");
            Rows[0][1] = InlineKeyboardButton.WithCallbackData("Off", "off");

            Rows[1] = new InlineKeyboardButton[1];
            Rows[1][0] = InlineKeyboardButton.WithCallbackData("Toggle", "toggle");

            Rows[2] = new InlineKeyboardButton[1];
            Rows[2][0] = InlineKeyboardButton.WithCallbackData("<<", "back");
           
            InlineKeyboardMarkup OnOffKeyboard = new InlineKeyboardMarkup(Rows);
            return OnOffKeyboard;
        }

        private ReplyKeyboardMarkup CreateHardwareToggles()
        {
            var Keyboard =
                    new KeyboardButton[][]
                    {
                        new KeyboardButton[]
                        {
                            new KeyboardButton(HardwareEmoji.LIGHT),
                            new KeyboardButton(HardwareEmoji.PC)
                        },
                        new KeyboardButton[]
                        {

                            new KeyboardButton(HardwareEmoji.PLUGS),
                            new KeyboardButton(HardwareEmoji.MONITOR)

                        },
                        new KeyboardButton[]
                        {
                            new KeyboardButton(HardwareEmoji.ON),
                        },
                        new KeyboardButton[]
                        {
                            new KeyboardButton(HardwareEmoji.OFF),
                        },
                        
                    };
            return new ReplyKeyboardMarkup(Keyboard);
        }

        private Task ErrorHandler(ITelegramBotClient Cortana, Exception exception, CancellationToken cancellationToken)
        {
            var ErrorMessage = exception switch
            {
                ApiRequestException apiRequestException => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                _ => exception.ToString()
            };
            string path = "Telegram Log.txt";
            using StreamWriter logFile = System.IO.File.Exists(path) ? System.IO.File.AppendText(path) : System.IO.File.CreateText(path);
            logFile.WriteLine($"{DateTime.Now} Exception: " + ErrorMessage);

            return Task.CompletedTask;
        }

        private IConfigurationRoot ConfigurationBuilder()
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("Data/Telegram/Token.json")
                .Build();
        }
    }
}