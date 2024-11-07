using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Processor;

namespace TelegramBot.Modules
{
    internal static class HardwareEmoji
    {
        public const string Bulb = "💡";
        public const string Pc = "🖥";
        public const string Thunder = "⚡";
        public const string Reboot = "🔄";
        public const string On = "🟩🟩🟩";
        public const string Off = "🟥🟥🟥";
    }
    
    public static class HardwareModule
    {
        private static readonly Dictionary<long, string> HardwareAction = new();
        
        public static async void ExecCommand(MessageStats messageStats, ITelegramBotClient cortana)
        {
            switch (messageStats.Command)
            {
                case "ip":
                    string ip = await Hardware.GetPublicIp();
                    await cortana.SendMessage(messageStats.ChatID, $"IP: {ip}");
                    break;
                case "gateway":
                    string gateway = Hardware.GetDefaultGateway();
                    await cortana.SendMessage(messageStats.ChatID, $"Gateway: {gateway}");
                    break;
                case "temperature":
                    string temp = Hardware.GetCpuTemperature();
                    await cortana.SendMessage(messageStats.ChatID, $"Temperature: {temp}");
                    break;
                case "hardware":
                    if (TelegramData.CheckPermission(messageStats.UserID))
                        await cortana.SendMessage(messageStats.ChatID, "Hardware Keyboard", replyMarkup: CreateHardwareButtons());
                    else
                        await cortana.SendMessage(messageStats.ChatID, "Not enough privileges");
                    break;
                case "keyboard":
                    if (TelegramData.CheckPermission(messageStats.UserID))
                        await cortana.SendMessage(messageStats.ChatID, "Hardware Toggle Keyboard", replyMarkup: CreateHardwareToggles());
                    else
                        await cortana.SendMessage(messageStats.ChatID, "Not enough privileges");
                    break;
                case "reboot":
                    if (TelegramData.CheckPermission(messageStats.UserID))
                    {
                        string res = Hardware.PowerRaspberry(EPowerOption.Reboot);
                        await cortana.SendMessage(messageStats.ChatID, res);
                    }
                    else await cortana.SendMessage(messageStats.ChatID, "Not enough privileges");
                    break;
                case "shutdown":
                    if (TelegramData.CheckPermission(messageStats.UserID))
                    {
                        string res = Hardware.PowerRaspberry(EPowerOption.Shutdown);
                        await cortana.SendMessage(messageStats.ChatID, res);
                    }
                    else await cortana.SendMessage(messageStats.ChatID, "Not enough privileges");
                    break;
                case "notify":
                    if (TelegramData.CheckPermission(messageStats.UserID))
                    {
                        string res = Hardware.NotifyPc(messageStats.Text);
                        if (res == "0") await cortana.DeleteMessage(messageStats.ChatID, messageStats.MessageID);
                        else await cortana.SendMessage(messageStats.ChatID, res);
                    }
                    else await cortana.SendMessage(messageStats.ChatID, "Not enough privileges");
                    break;
            }
        }
        
        public static async void HandleCallback(MessageStats messageStats, ITelegramBotClient cortana)
        {
            if (!TelegramData.CheckPermission(messageStats.UserID) || messageStats.ChatType != ChatType.Private) return;
            switch (messageStats.FullMessage)
            {
                case HardwareEmoji.Bulb:
                    Hardware.SwitchLamp(EHardwareTrigger.Toggle);
                    break;
                case HardwareEmoji.Pc:
                    Hardware.SwitchComputer(EHardwareTrigger.Toggle);
                    break;
                case HardwareEmoji.Thunder:
                    Hardware.SwitchGeneral(EHardwareTrigger.Toggle);
                    break;
                case HardwareEmoji.On:
                    Hardware.SwitchRoom(EHardwareTrigger.On);
                    break;
                case HardwareEmoji.Off:
                    Hardware.SwitchRoom(EHardwareTrigger.Off);
                    break;
                case HardwareEmoji.Reboot:
                    Hardware.RebootPc();
                    break;
                default:
                    if (messageStats.UserID != TelegramData.NameToId("@gwynn7")) return;
                    
                    string result = Hardware.SSH_PC(messageStats.FullMessage, returnResult:true);
                    await cortana.SendMessage(messageStats.ChatID, result);
                    
                    return;
            }
            await cortana.DeleteMessage(messageStats.ChatID, messageStats.MessageID);
        }
        
        public static async void ButtonCallback(ITelegramBotClient cortana, Update update)
        {
            if(update.CallbackQuery == null) return;

            string data = update.CallbackQuery.Data!;
            int messageId = update.CallbackQuery.Message!.MessageId;
            
            if(!data.StartsWith("hardware-")) return;
            data = data["hardware-".Length..];
            
            if (!TelegramData.CheckPermission(update.CallbackQuery!.From.Id))
            {
                await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);
                return;
            }
            
            InlineKeyboardMarkup action;

            if (HardwareAction.TryAdd(messageId, data))
            {
                action = CreateOnOffButtons();
            }
            else
            {
                if (data != "back") Hardware.SwitchFromString(HardwareAction[messageId], data);
                HardwareAction.Remove(messageId);
                action = CreateHardwareButtons();
            }

            await cortana.AnswerCallbackQuery(update.CallbackQuery.Id);
            await cortana.EditMessageReplyMarkup(update.CallbackQuery.Message.Chat.Id, messageId, action);
        }
        
        private static InlineKeyboardMarkup CreateHardwareButtons()
        {
            var rows = new InlineKeyboardButton[Enum.GetValues(typeof(EHardwareElements)).Length + 1][];

            rows[0] = new InlineKeyboardButton[1];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("Room", "hardware-room");

            var index = 1;
            foreach (string element in Enum.GetNames(typeof(EHardwareElements)))
            {
                rows[index] = new InlineKeyboardButton[1];
                rows[index][0] = InlineKeyboardButton.WithCallbackData(element, $"hardware-{element.ToLower()}");
                index++;
            }

            var hardwareKeyboard = new InlineKeyboardMarkup(rows);
            return hardwareKeyboard;
        }

        private static InlineKeyboardMarkup CreateOnOffButtons()
        {
            var rows = new InlineKeyboardButton[3][];

            rows[0] = new InlineKeyboardButton[2];
            rows[0][0] = InlineKeyboardButton.WithCallbackData("On", "hardware-on");
            rows[0][1] = InlineKeyboardButton.WithCallbackData("Off", "hardware-off");

            rows[1] = new InlineKeyboardButton[1];
            rows[1][0] = InlineKeyboardButton.WithCallbackData("Toggle", "hardware-toggle");

            rows[2] = new InlineKeyboardButton[1];
            rows[2][0] = InlineKeyboardButton.WithCallbackData("<<", "hardware-back");

            var keyboard = new InlineKeyboardMarkup(rows);
            return keyboard;
        }

        private static ReplyKeyboardMarkup CreateHardwareToggles()
        {
            var keyboard =
                new KeyboardButton[][]
                {
                    [
                        new KeyboardButton(HardwareEmoji.Bulb),
                        new KeyboardButton(HardwareEmoji.Thunder)
                    ],
                    [
                        new KeyboardButton(HardwareEmoji.Pc),
                        new KeyboardButton(HardwareEmoji.Reboot)
                    ],
                    [
                        new KeyboardButton(HardwareEmoji.On),
                    ],
                    [
                        new KeyboardButton(HardwareEmoji.Off),
                    ],

                };
            return new ReplyKeyboardMarkup(keyboard);
        }
    }
}